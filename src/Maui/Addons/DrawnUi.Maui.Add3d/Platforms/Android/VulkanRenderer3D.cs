#nullable disable
using System.Numerics;
using System.Runtime.CompilerServices;
using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;
using static DrawnUi.Add3d.Vk3d;

namespace DrawnUi.Add3d;

/// <summary>
/// The 3D renderer on a canvas drawing with Vulkan (DrawnUi.Maui on Android by default): the same shaders as the GL
/// renderer (Platforms/Android/Shaders, compiled to SPIR-V), drawn on the canvas's own device and queue. Each control
/// records one command buffer into its own image with a depth buffer and submits it on the canvas's render thread
/// while the canvas draws, so it runs before the work Skia submits for that frame; Skia then samples the image.
/// The render pass moves the image UNDEFINED -> COLOR_ATTACHMENT_OPTIMAL -> SHADER_READ_ONLY_OPTIMAL, the layout Skia
/// is told.
/// </summary>
internal sealed unsafe class VulkanRenderer3D
{
    const int MaxJoints = 64, DrawBlockSize = 256, JointsBlockSize = MaxJoints * 64, Slots = 32;

    /// <summary>The uniform block of one draw, std140 (model.vert / model.frag "Draw").</summary>
    struct DrawBlock
    {
        public Matrix4x4 Model, ViewProj;
        public Vector4 Color, Light, OutlineColor, Params;
        public int Skinned, Textured, Look, OutlinePass;
    }

    /// <summary>A control's frame on Vulkan, in <see cref="RenderTarget.Native"/>.</summary>
    sealed class VkTarget
    {
        /// <summary>The renderer of the canvas it was made on; it frees the target on that canvas's render thread.</summary>
        public VulkanRenderer3D Owner;
        public IntPtr Device;
        public ulong Color, ColorMemory, ColorView, Depth, DepthMemory, DepthView, Framebuffer;
        public ulong ColorSize;
    }

    sealed class GpuTexture
    {
        public ulong Image, Memory, View, Set;
    }

    /// <summary>One model on the GPU: its vertex data in one buffer, per mesh, per primitive the offsets of each attribute.</summary>
    sealed class GpuModel
    {
        public ulong Buffer, Memory;
        public ulong[][][] Offsets; // [mesh][primitive][0..5 attributes, 6 indices]
        public int[][] Counts;
        public GpuTexture[] Textures;
    }

    /// <summary>One submission: its command buffer, its fence and the uniform data its draws read.</summary>
    sealed class Slot
    {
        public IntPtr Commands;
        public ulong Fence, Buffer, Memory, Set;
        public byte* Mapped;
        public ulong Size;
    }

    IntPtr _device, _physical, _queue;
    uint _family;
    uint _depthFormat;
    ulong _renderPass, _setLayoutDraw, _setLayoutTexture, _pipelineLayout, _pool, _descriptorPool, _sampler;
    readonly ulong[] _pipelines = new ulong[6]; // cull (none, back, front) * 2 + blend
    Slot[] _slots;
    int _nextSlot;
    GpuTexture _white;
    uint[] _memoryTypes;
    ConditionalWeakTable<Scene3D, GpuModel> _models = new();
    // ponytail: like models, the texture of an image that is gone is freed only with the device.
    /// <summary>Images put on materials from code (Texture3D), by their pixels: uploaded once per device.</summary>
    ConditionalWeakTable<Image3D, GpuTexture> _images = new();
    readonly List<RenderTarget> _released = new();
    readonly Matrix4x4[] _joints = new Matrix4x4[MaxJoints];
    static readonly Vector3 Light = Vector3.Normalize(new Vector3(0.4f, 0.8f, 0.6f));
    bool _failed;

    /// <summary>
    /// One renderer per canvas view: each Vulkan canvas has its own device and render thread, so a renderer's objects are
    /// made, used and freed only on its canvas's render thread.
    /// </summary>
    static readonly ConditionalWeakTable<SkiaVulkanTextureView, VulkanRenderer3D> Renderers = new();

    /// <summary>Frees a control's images at the next draw of its canvas (on that canvas's render thread).</summary>
    public static void Release(RenderTarget target)
    {
        target.Image?.Dispose();
        target.Image = null;
        if (target.Native is VkTarget { Owner: { } owner })
            lock (owner._released)
                owner._released.Add(target);
    }

    /// <summary>Draws the objects into the control's image on the canvas's Vulkan device; null when it cannot.</summary>
    public static SKImage Draw(DrawnView canvas, GRContext gr, RenderTarget target, RenderItem[] items, int count, in RenderStyle style, in Matrix4x4 viewProj, int w, int h)
    {
        if ((canvas?.CanvasView as Microsoft.Maui.Controls.View)?.Handler?.PlatformView is not SkiaVulkanTextureView view)
            return null;
        return Renderers.GetValue(view, _ => new VulkanRenderer3D()).DrawOn(view, gr, target, items, count, style, viewProj, w, h);
    }

    SKImage DrawOn(SkiaVulkanTextureView view, GRContext gr, RenderTarget target, RenderItem[] items, int count, in RenderStyle style, in Matrix4x4 viewProj, int w, int h)
    {
        if (_failed)
            return null;
        var device = view.VkDevice;
        if (device == IntPtr.Zero)
            return null;
        try
        {
            if (device != _device)
                Init(view);
            if (_released.Count > 0)
                DeleteReleased();
            var t = EnsureTarget(gr, target, w, h);
            Render(t, items, count, style, viewProj, w, h);
            return target.Image;
        }
        catch (Exception e)
        {
            // ponytail: one failure turns the 3D off on this canvas; a device lost mid-frame is the likely cause.
            _failed = true;
            Super.Log($"[SkiaModel3D] Vulkan: {e.Message}");
            return null;
        }
    }

    static void Check(int result, string what)
    {
        if (result != SUCCESS)
            throw new InvalidOperationException($"{what} failed ({result})");
    }

    #region Device objects

    /// <summary>A new device (the first one, or the canvas made a new one): everything is made again on it.</summary>
    void Init(SkiaVulkanTextureView view)
    {
        // The objects of an old device died with it.
        _device = view.VkDevice;
        _physical = view.VkPhysicalDevice;
        _queue = view.VkQueue;
        _family = view.VkQueueFamily;
        _models = new ConditionalWeakTable<Scene3D, GpuModel>();
        _images = new ConditionalWeakTable<Image3D, GpuTexture>();
        lock (_released)
            _released.Clear();

        var props = stackalloc byte[520];
        vkGetPhysicalDeviceMemoryProperties(_physical, props);
        var typeCount = *(uint*)props;
        _memoryTypes = new uint[typeCount];
        for (var i = 0; i < typeCount; i++)
            _memoryTypes[i] = *(uint*)(props + 4 + i * 8);

        _depthFormat = FORMAT_D16_UNORM;
        foreach (var f in new[] { FORMAT_D32_SFLOAT, FORMAT_X8_D24_UNORM_PACK32 })
        {
            FormatProperties fp;
            vkGetPhysicalDeviceFormatProperties(_physical, f, &fp);
            if ((fp.optimalTilingFeatures & FORMAT_FEATURE_DEPTH_STENCIL_ATTACHMENT_BIT) != 0)
            {
                _depthFormat = f;
                break;
            }
        }

        MakeRenderPass();
        MakeLayouts();
        MakePipelines();

        var poolInfo = new CommandPoolCreateInfo { sType = STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO, flags = COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT, queueFamilyIndex = _family };
        fixed (ulong* p = &_pool)
            Check(vkCreateCommandPool(_device, &poolInfo, IntPtr.Zero, p), "command pool");

        var sizes = stackalloc DescriptorPoolSize[2];
        sizes[0] = new DescriptorPoolSize { type = DESCRIPTOR_TYPE_UNIFORM_BUFFER_DYNAMIC, descriptorCount = Slots * 2 };
        sizes[1] = new DescriptorPoolSize { type = DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER, descriptorCount = 1024 };
        var dpInfo = new DescriptorPoolCreateInfo { sType = STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO, maxSets = Slots + 1024, poolSizeCount = 2, pPoolSizes = sizes };
        fixed (ulong* p = &_descriptorPool)
            Check(vkCreateDescriptorPool(_device, &dpInfo, IntPtr.Zero, p), "descriptor pool");

        var sInfo = new SamplerCreateInfo
        {
            sType = STRUCTURE_TYPE_SAMPLER_CREATE_INFO, magFilter = FILTER_LINEAR, minFilter = FILTER_LINEAR, mipmapMode = SAMPLER_MIPMAP_MODE_LINEAR,
            addressModeU = SAMPLER_ADDRESS_MODE_REPEAT, addressModeV = SAMPLER_ADDRESS_MODE_REPEAT, addressModeW = SAMPLER_ADDRESS_MODE_REPEAT,
            maxAnisotropy = 1, maxLod = 16,
        };
        fixed (ulong* p = &_sampler)
            Check(vkCreateSampler(_device, &sInfo, IntPtr.Zero, p), "sampler");

        _slots = new Slot[Slots];
        _nextSlot = 0;
        _white = UploadTexture(new Image3D { Width = 1, Height = 1, Rgba = new byte[] { 255, 255, 255, 255 } });
    }

    uint MemoryType(uint bits, uint flags)
    {
        for (var i = 0; i < _memoryTypes.Length; i++)
            if ((bits & (1u << i)) != 0 && (_memoryTypes[i] & flags) == flags)
                return (uint)i;
        if ((flags & MEMORY_PROPERTY_DEVICE_LOCAL_BIT) != 0)
            return MemoryType(bits, flags & ~MEMORY_PROPERTY_DEVICE_LOCAL_BIT);
        throw new InvalidOperationException("no memory type");
    }

    ulong Allocate(MemoryRequirements req, uint flags)
    {
        var info = new MemoryAllocateInfo { sType = STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO, allocationSize = req.size, memoryTypeIndex = MemoryType(req.memoryTypeBits, flags) };
        ulong memory;
        Check(vkAllocateMemory(_device, &info, IntPtr.Zero, &memory), "memory");
        return memory;
    }

    ulong HostBuffer(ulong size, uint usage, out ulong memory, out byte* mapped)
    {
        var info = new BufferCreateInfo { sType = STRUCTURE_TYPE_BUFFER_CREATE_INFO, size = size, usage = usage };
        ulong buffer;
        Check(vkCreateBuffer(_device, &info, IntPtr.Zero, &buffer), "buffer");
        MemoryRequirements req;
        vkGetBufferMemoryRequirements(_device, buffer, &req);
        memory = Allocate(req, MEMORY_PROPERTY_HOST_VISIBLE_BIT | MEMORY_PROPERTY_HOST_COHERENT_BIT);
        Check(vkBindBufferMemory(_device, buffer, memory, 0), "bind buffer");
        void* data;
        Check(vkMapMemory(_device, memory, 0, ~0ul, 0, &data), "map");
        mapped = (byte*)data;
        return buffer;
    }

    (ulong image, ulong memory, ulong view) Image(uint w, uint h, uint levels, uint format, uint usage, uint aspect)
    {
        var info = new ImageCreateInfo
        {
            sType = STRUCTURE_TYPE_IMAGE_CREATE_INFO, imageType = 1, format = format, extent = new Extent3D { width = w, height = h, depth = 1 },
            mipLevels = levels, arrayLayers = 1, samples = 1, usage = usage, initialLayout = IMAGE_LAYOUT_UNDEFINED,
        };
        ulong image;
        Check(vkCreateImage(_device, &info, IntPtr.Zero, &image), "image");
        MemoryRequirements req;
        vkGetImageMemoryRequirements(_device, image, &req);
        var memory = Allocate(req, MEMORY_PROPERTY_DEVICE_LOCAL_BIT);
        Check(vkBindImageMemory(_device, image, memory, 0), "bind image");
        var vInfo = new ImageViewCreateInfo
        {
            sType = STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO, image = image, viewType = 1, format = format,
            subresourceRange = new ImageSubresourceRange { aspectMask = aspect, levelCount = levels, layerCount = 1 },
        };
        ulong view;
        Check(vkCreateImageView(_device, &vInfo, IntPtr.Zero, &view), "image view");
        return (image, memory, view);
    }

    void MakeRenderPass()
    {
        var attachments = stackalloc AttachmentDescription[2];
        attachments[0] = new AttachmentDescription
        {
            format = FORMAT_R8G8B8A8_UNORM, samples = 1, loadOp = ATTACHMENT_LOAD_OP_CLEAR, storeOp = ATTACHMENT_STORE_OP_STORE,
            stencilLoadOp = ATTACHMENT_LOAD_OP_DONT_CARE, stencilStoreOp = ATTACHMENT_STORE_OP_DONT_CARE,
            initialLayout = IMAGE_LAYOUT_UNDEFINED, finalLayout = IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
        };
        attachments[1] = new AttachmentDescription
        {
            format = _depthFormat, samples = 1, loadOp = ATTACHMENT_LOAD_OP_CLEAR, storeOp = ATTACHMENT_STORE_OP_DONT_CARE,
            stencilLoadOp = ATTACHMENT_LOAD_OP_DONT_CARE, stencilStoreOp = ATTACHMENT_STORE_OP_DONT_CARE,
            initialLayout = IMAGE_LAYOUT_UNDEFINED, finalLayout = IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL,
        };
        var color = new AttachmentReference { attachment = 0, layout = IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL };
        var depth = new AttachmentReference { attachment = 1, layout = IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL };
        var subpass = new SubpassDescription { pipelineBindPoint = PIPELINE_BIND_POINT_GRAPHICS, colorAttachmentCount = 1, pColorAttachments = &color, pDepthStencilAttachment = &depth };
        var deps = stackalloc SubpassDependency[2];
        // Before: Skia's reads of the last frame and our last depth writes are done.
        deps[0] = new SubpassDependency
        {
            srcSubpass = SUBPASS_EXTERNAL, dstSubpass = 0,
            srcStageMask = PIPELINE_STAGE_FRAGMENT_SHADER_BIT | PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT,
            dstStageMask = PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT | PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT,
            srcAccessMask = ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT,
            dstAccessMask = ACCESS_COLOR_ATTACHMENT_WRITE_BIT | ACCESS_DEPTH_STENCIL_ATTACHMENT_READ_BIT | ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT,
        };
        // After: Skia's fragment shaders read what we wrote.
        deps[1] = new SubpassDependency
        {
            srcSubpass = 0, dstSubpass = SUBPASS_EXTERNAL,
            srcStageMask = PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT, dstStageMask = PIPELINE_STAGE_FRAGMENT_SHADER_BIT,
            srcAccessMask = ACCESS_COLOR_ATTACHMENT_WRITE_BIT, dstAccessMask = ACCESS_SHADER_READ_BIT,
        };
        var info = new RenderPassCreateInfo
        {
            sType = STRUCTURE_TYPE_RENDER_PASS_CREATE_INFO, attachmentCount = 2, pAttachments = attachments, subpassCount = 1, pSubpasses = &subpass,
            dependencyCount = 2, pDependencies = deps,
        };
        fixed (ulong* p = &_renderPass)
            Check(vkCreateRenderPass(_device, &info, IntPtr.Zero, p), "render pass");
    }

    void MakeLayouts()
    {
        var draw = stackalloc DescriptorSetLayoutBinding[2];
        draw[0] = new DescriptorSetLayoutBinding { binding = 0, descriptorType = DESCRIPTOR_TYPE_UNIFORM_BUFFER_DYNAMIC, descriptorCount = 1, stageFlags = SHADER_STAGE_VERTEX_BIT | SHADER_STAGE_FRAGMENT_BIT };
        draw[1] = new DescriptorSetLayoutBinding { binding = 1, descriptorType = DESCRIPTOR_TYPE_UNIFORM_BUFFER_DYNAMIC, descriptorCount = 1, stageFlags = SHADER_STAGE_VERTEX_BIT };
        var info = new DescriptorSetLayoutCreateInfo { sType = STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO, bindingCount = 2, pBindings = draw };
        fixed (ulong* p = &_setLayoutDraw)
            Check(vkCreateDescriptorSetLayout(_device, &info, IntPtr.Zero, p), "set layout");
        var tex = new DescriptorSetLayoutBinding { binding = 0, descriptorType = DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER, descriptorCount = 1, stageFlags = SHADER_STAGE_FRAGMENT_BIT };
        info = new DescriptorSetLayoutCreateInfo { sType = STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO, bindingCount = 1, pBindings = &tex };
        fixed (ulong* p = &_setLayoutTexture)
            Check(vkCreateDescriptorSetLayout(_device, &info, IntPtr.Zero, p), "set layout");
        var layouts = stackalloc ulong[] { _setLayoutDraw, _setLayoutTexture };
        var pInfo = new PipelineLayoutCreateInfo { sType = STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO, setLayoutCount = 2, pSetLayouts = layouts };
        fixed (ulong* p = &_pipelineLayout)
            Check(vkCreatePipelineLayout(_device, &pInfo, IntPtr.Zero, p), "pipeline layout");
    }

    ulong ShaderModule(string name)
    {
        using var stream = typeof(VulkanRenderer3D).Assembly.GetManifestResourceStream(name)
                           ?? throw new InvalidOperationException($"no shader {name}");
        var code = new byte[stream.Length];
        stream.ReadExactly(code);
        fixed (byte* p = code)
        {
            var info = new ShaderModuleCreateInfo { sType = STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO, codeSize = (nuint)code.Length, pCode = (uint*)p };
            ulong module;
            Check(vkCreateShaderModule(_device, &info, IntPtr.Zero, &module), "shader module");
            return module;
        }
    }

    void MakePipelines()
    {
        var vs = ShaderModule("model.vert.spv");
        var fs = ShaderModule("model.frag.spv");
        var main = stackalloc byte[] { (byte)'m', (byte)'a', (byte)'i', (byte)'n', 0 };
        var stages = stackalloc PipelineShaderStageCreateInfo[2];
        stages[0] = new PipelineShaderStageCreateInfo { sType = STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO, stage = SHADER_STAGE_VERTEX_BIT, module = vs, pName = main };
        stages[1] = new PipelineShaderStageCreateInfo { sType = STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO, stage = SHADER_STAGE_FRAGMENT_BIT, module = fs, pName = main };

        // One buffer per attribute, as the GL renderer: position, normal, uv, joints, weights, outline normal.
        var bindings = stackalloc VertexInputBindingDescription[6];
        var attributes = stackalloc VertexInputAttributeDescription[6];
        var sizes = stackalloc uint[] { 3, 3, 2, 4, 4, 3 };
        for (uint i = 0; i < 6; i++)
        {
            bindings[i] = new VertexInputBindingDescription { binding = i, stride = sizes[i] * 4 };
            attributes[i] = new VertexInputAttributeDescription
            {
                location = i, binding = i,
                format = sizes[i] == 2 ? FORMAT_R32G32_SFLOAT : sizes[i] == 3 ? FORMAT_R32G32B32_SFLOAT : FORMAT_R32G32B32A32_SFLOAT,
            };
        }
        var vertexInput = new PipelineVertexInputStateCreateInfo
        {
            sType = STRUCTURE_TYPE_PIPELINE_VERTEX_INPUT_STATE_CREATE_INFO, vertexBindingDescriptionCount = 6, pVertexBindingDescriptions = bindings,
            vertexAttributeDescriptionCount = 6, pVertexAttributeDescriptions = attributes,
        };
        var assembly = new PipelineInputAssemblyStateCreateInfo { sType = STRUCTURE_TYPE_PIPELINE_INPUT_ASSEMBLY_STATE_CREATE_INFO, topology = PRIMITIVE_TOPOLOGY_TRIANGLE_LIST };
        var viewport = new PipelineViewportStateCreateInfo { sType = STRUCTURE_TYPE_PIPELINE_VIEWPORT_STATE_CREATE_INFO, viewportCount = 1, scissorCount = 1 };
        var multisample = new PipelineMultisampleStateCreateInfo { sType = STRUCTURE_TYPE_PIPELINE_MULTISAMPLE_STATE_CREATE_INFO, rasterizationSamples = 1 };
        var dynamic = stackalloc uint[] { DYNAMIC_STATE_VIEWPORT, DYNAMIC_STATE_SCISSOR };
        var dynamicState = new PipelineDynamicStateCreateInfo { sType = STRUCTURE_TYPE_PIPELINE_DYNAMIC_STATE_CREATE_INFO, dynamicStateCount = 2, pDynamicStates = dynamic };

        for (var cull = 0; cull < 3; cull++)
        for (var blend = 0; blend < 2; blend++)
        {
            // The image keeps GL's row order (shown bottom-left first), so GL's counter-clockwise front faces are
            // clockwise in Vulkan's framebuffer space.
            var raster = new PipelineRasterizationStateCreateInfo
            {
                sType = STRUCTURE_TYPE_PIPELINE_RASTERIZATION_STATE_CREATE_INFO,
                cullMode = cull == 0 ? CULL_MODE_NONE : cull == 1 ? CULL_MODE_BACK_BIT : CULL_MODE_FRONT_BIT,
                frontFace = FRONT_FACE_CLOCKWISE, lineWidth = 1,
            };
            var depth = new PipelineDepthStencilStateCreateInfo
            {
                sType = STRUCTURE_TYPE_PIPELINE_DEPTH_STENCIL_STATE_CREATE_INFO, depthTestEnable = 1, depthWriteEnable = blend == 0 ? 1u : 0u,
                depthCompareOp = COMPARE_OP_LESS, maxDepthBounds = 1,
            };
            var attachment = new PipelineColorBlendAttachmentState
            {
                blendEnable = (uint)blend, srcColorBlendFactor = BLEND_FACTOR_ONE, dstColorBlendFactor = BLEND_FACTOR_ONE_MINUS_SRC_ALPHA, colorBlendOp = BLEND_OP_ADD,
                srcAlphaBlendFactor = BLEND_FACTOR_ONE, dstAlphaBlendFactor = BLEND_FACTOR_ONE_MINUS_SRC_ALPHA, alphaBlendOp = BLEND_OP_ADD, colorWriteMask = COLOR_COMPONENT_ALL,
            };
            var colorBlend = new PipelineColorBlendStateCreateInfo { sType = STRUCTURE_TYPE_PIPELINE_COLOR_BLEND_STATE_CREATE_INFO, attachmentCount = 1, pAttachments = &attachment };
            var info = new GraphicsPipelineCreateInfo
            {
                sType = STRUCTURE_TYPE_GRAPHICS_PIPELINE_CREATE_INFO, stageCount = 2, pStages = stages, pVertexInputState = &vertexInput,
                pInputAssemblyState = &assembly, pViewportState = &viewport, pRasterizationState = &raster, pMultisampleState = &multisample,
                pDepthStencilState = &depth, pColorBlendState = &colorBlend, pDynamicState = &dynamicState, layout = _pipelineLayout,
                renderPass = _renderPass, basePipelineIndex = -1,
            };
            fixed (ulong* p = &_pipelines[cull * 2 + blend])
                Check(vkCreateGraphicsPipelines(_device, 0, 1, &info, IntPtr.Zero, p), "pipeline");
        }
    }

    #endregion

    #region Uploads

    /// <summary>Records, submits and waits for one command buffer: model uploads only, never per frame.</summary>
    void OneShot(Action<IntPtr> record)
    {
        var slot = NextSlot();
        record(slot.Commands);
        Check(vkEndCommandBuffer(slot.Commands), "end");
        Submit(slot);
        var fence = slot.Fence;
        vkWaitForFences(_device, 1, &fence, 1, ulong.MaxValue);
    }

    GpuTexture UploadTexture(Image3D source)
    {
        var w = (uint)source.Width;
        var h = (uint)source.Height;
        var levels = (uint)Math.Floor(Math.Log2(Math.Max(w, h))) + 1;
        var (image, memory, view) = Image(w, h, levels, FORMAT_R8G8B8A8_UNORM,
            IMAGE_USAGE_SAMPLED_BIT | IMAGE_USAGE_TRANSFER_DST_BIT | IMAGE_USAGE_TRANSFER_SRC_BIT, IMAGE_ASPECT_COLOR_BIT);
        var staging = HostBuffer((ulong)source.Rgba.Length, BUFFER_USAGE_TRANSFER_SRC_BIT, out var stagingMemory, out var mapped);
        fixed (byte* p = source.Rgba)
            Buffer.MemoryCopy(p, mapped, source.Rgba.Length, source.Rgba.Length);

        OneShot(cb =>
        {
            Barrier(cb, image, 0, levels, IMAGE_LAYOUT_UNDEFINED, IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, 0, ACCESS_TRANSFER_WRITE_BIT,
                PIPELINE_STAGE_TOP_OF_PIPE_BIT, PIPELINE_STAGE_TRANSFER_BIT);
            var copy = new BufferImageCopy
            {
                imageSubresource = new ImageSubresourceLayers { aspectMask = IMAGE_ASPECT_COLOR_BIT, layerCount = 1 },
                imageExtent = new Extent3D { width = w, height = h, depth = 1 },
            };
            vkCmdCopyBufferToImage(cb, staging, image, IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, 1, &copy);
            // Mipmaps, each level blitted from the one above, as glGenerateMipmap.
            int mw = (int)w, mh = (int)h;
            for (uint level = 1; level < levels; level++)
            {
                Barrier(cb, image, level - 1, 1, IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL,
                    ACCESS_TRANSFER_WRITE_BIT, ACCESS_TRANSFER_READ_BIT, PIPELINE_STAGE_TRANSFER_BIT, PIPELINE_STAGE_TRANSFER_BIT);
                var blit = new ImageBlit
                {
                    srcSubresource = new ImageSubresourceLayers { aspectMask = IMAGE_ASPECT_COLOR_BIT, mipLevel = level - 1, layerCount = 1 },
                    src1 = new Offset3D { x = mw, y = mh, z = 1 },
                    dstSubresource = new ImageSubresourceLayers { aspectMask = IMAGE_ASPECT_COLOR_BIT, mipLevel = level, layerCount = 1 },
                    dst1 = new Offset3D { x = Math.Max(1, mw / 2), y = Math.Max(1, mh / 2), z = 1 },
                };
                vkCmdBlitImage(cb, image, IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, image, IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, 1, &blit, FILTER_LINEAR);
                Barrier(cb, image, level - 1, 1, IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL, IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                    ACCESS_TRANSFER_READ_BIT, ACCESS_SHADER_READ_BIT, PIPELINE_STAGE_TRANSFER_BIT, PIPELINE_STAGE_FRAGMENT_SHADER_BIT);
                mw = Math.Max(1, mw / 2);
                mh = Math.Max(1, mh / 2);
            }
            Barrier(cb, image, levels - 1, 1, IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL, IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
                ACCESS_TRANSFER_WRITE_BIT, ACCESS_SHADER_READ_BIT, PIPELINE_STAGE_TRANSFER_BIT, PIPELINE_STAGE_FRAGMENT_SHADER_BIT);
        });
        vkDestroyBuffer(_device, staging, IntPtr.Zero);
        vkFreeMemory(_device, stagingMemory, IntPtr.Zero);

        var texture = new GpuTexture { Image = image, Memory = memory, View = view };
        var layout = _setLayoutTexture;
        var alloc = new DescriptorSetAllocateInfo { sType = STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO, descriptorPool = _descriptorPool, descriptorSetCount = 1, pSetLayouts = &layout };
        ulong set;
        Check(vkAllocateDescriptorSets(_device, &alloc, &set), "texture set");
        var imageInfo = new DescriptorImageInfo { sampler = _sampler, imageView = view, imageLayout = IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL };
        var write = new WriteDescriptorSet
        {
            sType = STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET, dstSet = set, descriptorCount = 1, descriptorType = DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER, pImageInfo = &imageInfo,
        };
        vkUpdateDescriptorSets(_device, 1, &write, 0, IntPtr.Zero);
        texture.Set = set;
        return texture;
    }

    static void Barrier(IntPtr cb, ulong image, uint level, uint levels, uint from, uint to, uint srcAccess, uint dstAccess, uint srcStage, uint dstStage)
    {
        var barrier = new ImageMemoryBarrier
        {
            sType = STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER, srcAccessMask = srcAccess, dstAccessMask = dstAccess, oldLayout = from, newLayout = to,
            srcQueueFamilyIndex = QUEUE_FAMILY_IGNORED, dstQueueFamilyIndex = QUEUE_FAMILY_IGNORED, image = image,
            subresourceRange = new ImageSubresourceRange { aspectMask = IMAGE_ASPECT_COLOR_BIT, baseMipLevel = level, levelCount = levels, layerCount = 1 },
        };
        vkCmdPipelineBarrier(cb, srcStage, dstStage, 0, 0, IntPtr.Zero, 0, IntPtr.Zero, 1, &barrier);
    }

    GpuModel Upload(Scene3D scene)
    {
        var model = new GpuModel
        {
            Textures = new GpuTexture[scene.Images.Length],
            Offsets = new ulong[scene.Meshes.Length][][],
            Counts = new int[scene.Meshes.Length][],
        };
        for (var i = 0; i < scene.Images.Length; i++)
            model.Textures[i] = UploadTexture(scene.Images[i]);

        // All vertex and index data of the model in one buffer; attributes a primitive lacks get the values GL's
        // generic attributes give (uv and joints 0, weight 1 on the first joint).
        static ulong Align(ulong v) => (v + 15) & ~15ul;
        ulong total = 0;
        foreach (var primitives in scene.Meshes)
        foreach (var p in primitives)
        {
            var n = (ulong)p.Positions.Length;
            total += Align(n * 12) * 3 + Align(n * 8) + Align(n * 16) * 2 + Align((ulong)p.Indices.Length * 4);
        }
        var buffer = HostBuffer(Math.Max(16, total), BUFFER_USAGE_VERTEX_BUFFER_BIT | BUFFER_USAGE_INDEX_BUFFER_BIT, out var memory, out var mapped);
        ulong at = 0;
        ulong Put<T>(T[] data, int count, T fill) where T : unmanaged
        {
            var start = at;
            var dst = (T*)(mapped + at);
            if (data != null && data.Length == count)
                fixed (T* src = data)
                    Buffer.MemoryCopy(src, dst, (long)count * sizeof(T), (long)count * sizeof(T));
            else
                for (var i = 0; i < count; i++)
                    dst[i] = fill;
            at = Align(at + (ulong)(count * sizeof(T)));
            return start;
        }
        for (var m = 0; m < scene.Meshes.Length; m++)
        {
            var primitives = scene.Meshes[m];
            model.Offsets[m] = new ulong[primitives.Length][];
            model.Counts[m] = new int[primitives.Length];
            for (var k = 0; k < primitives.Length; k++)
            {
                var p = primitives[k];
                var n = p.Positions.Length;
                model.Offsets[m][k] = new[]
                {
                    Put(p.Positions, n, Vector3.Zero),
                    Put(p.Normals, n, Vector3.Zero),
                    Put(p.Uvs, n, Vector2.Zero),
                    Put(p.Joints, n, Vector4.Zero),
                    Put(p.Weights, n, new Vector4(1, 0, 0, 0)),
                    Put(Renderer3D.OutlineNormals(p), n, Vector3.Zero),
                    Put(p.Indices, p.Indices.Length, 0u),
                };
                model.Counts[m][k] = p.Indices.Length;
            }
        }
        model.Buffer = buffer;
        model.Memory = memory;
        return model;
    }

    /// <summary>The texture of an image put on a material, uploaded the first time it is drawn.</summary>
    GpuTexture ImageTexture(Image3D image)
    {
        if (!_images.TryGetValue(image, out var texture))
        {
            texture = UploadTexture(image);
            _images.AddOrUpdate(image, texture);
        }
        return texture;
    }

    GpuModel Model(Scene3D scene)
    {
        if (!_models.TryGetValue(scene, out var model))
        {
            model = Upload(scene);
            _models.AddOrUpdate(scene, model);
        }
        return model;
    }

    #endregion

    #region Frames

    VkTarget EnsureTarget(GRContext gr, RenderTarget target, int w, int h)
    {
        var t = target.Native as VkTarget;
        if (t != null && t.Owner != this)
        {
            // The control moved to another canvas: its old objects are freed by the old canvas.
            Release(target);
            t = null;
        }
        if (t != null && t.Device == _device && target.Width == w && target.Height == h && target.Image != null)
            return t;
        target.Image?.Dispose();
        target.Image = null;
        if (t != null && t.Device == _device)
        {
            // A resize: the GPU may still read the old images.
            vkQueueWaitIdle(_queue);
            Destroy(t);
        }
        t = new VkTarget { Owner = this, Device = _device };
        target.Native = t;
        target.Context = gr;
        (t.Color, t.ColorMemory, t.ColorView) = Image((uint)w, (uint)h, 1, FORMAT_R8G8B8A8_UNORM,
            IMAGE_USAGE_COLOR_ATTACHMENT_BIT | IMAGE_USAGE_SAMPLED_BIT | IMAGE_USAGE_TRANSFER_SRC_BIT | IMAGE_USAGE_TRANSFER_DST_BIT, IMAGE_ASPECT_COLOR_BIT);
        MemoryRequirements req;
        vkGetImageMemoryRequirements(_device, t.Color, &req);
        t.ColorSize = req.size;
        (t.Depth, t.DepthMemory, t.DepthView) = Image((uint)w, (uint)h, 1, _depthFormat, IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT, IMAGE_ASPECT_DEPTH_BIT);
        var views = stackalloc ulong[] { t.ColorView, t.DepthView };
        var info = new FramebufferCreateInfo
        {
            sType = STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO, renderPass = _renderPass, attachmentCount = 2, pAttachments = views,
            width = (uint)w, height = (uint)h, layers = 1,
        };
        ulong framebuffer;
        Check(vkCreateFramebuffer(_device, &info, IntPtr.Zero, &framebuffer), "framebuffer");
        t.Framebuffer = framebuffer;
        target.Width = w;
        target.Height = h;

        // Skia samples the image in the layout the render pass leaves it in; the rows are in GL's order, so it is
        // shown bottom-left first like the GL texture.
        var imageInfo = new GRVkImageInfo
        {
            Image = t.Color,
            Alloc = new GRVkAlloc { Memory = t.ColorMemory, Offset = 0, Size = t.ColorSize },
            ImageTiling = 0,
            ImageLayout = IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL,
            Format = FORMAT_R8G8B8A8_UNORM,
            ImageUsageFlags = IMAGE_USAGE_COLOR_ATTACHMENT_BIT | IMAGE_USAGE_SAMPLED_BIT | IMAGE_USAGE_TRANSFER_SRC_BIT | IMAGE_USAGE_TRANSFER_DST_BIT,
            SampleCount = 1,
            LevelCount = 1,
            CurrentQueueFamily = _family,
            SharingMode = 0,
        };
        using var backend = new GRBackendTexture(w, h, imageInfo);
        target.Image = SKImage.FromTexture(gr, backend, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888, SKAlphaType.Premul);
        return t;
    }

    void Destroy(VkTarget t)
    {
        vkDestroyFramebuffer(_device, t.Framebuffer, IntPtr.Zero);
        vkDestroyImageView(_device, t.ColorView, IntPtr.Zero);
        vkDestroyImage(_device, t.Color, IntPtr.Zero);
        vkFreeMemory(_device, t.ColorMemory, IntPtr.Zero);
        vkDestroyImageView(_device, t.DepthView, IntPtr.Zero);
        vkDestroyImage(_device, t.Depth, IntPtr.Zero);
        vkFreeMemory(_device, t.DepthMemory, IntPtr.Zero);
    }

    void DeleteReleased()
    {
        lock (_released)
        {
            var waited = false;
            foreach (var target in _released)
            {
                if (target.Native is not VkTarget t || t.Device != _device)
                    continue;
                if (!waited)
                {
                    vkQueueWaitIdle(_queue);
                    waited = true;
                }
                Destroy(t);
                target.Native = null;
            }
            _released.Clear();
        }
    }

    /// <summary>The next slot, its last submission finished; its command buffer reset and begun.</summary>
    Slot NextSlot()
    {
        var index = _nextSlot;
        _nextSlot = (_nextSlot + 1) % Slots;
        var slot = _slots[index];
        if (slot == null)
        {
            slot = new Slot();
            var alloc = new CommandBufferAllocateInfo { sType = STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO, commandPool = _pool, commandBufferCount = 1 };
            IntPtr cb;
            Check(vkAllocateCommandBuffers(_device, &alloc, &cb), "command buffer");
            slot.Commands = cb;
            var fInfo = new FenceCreateInfo { sType = STRUCTURE_TYPE_FENCE_CREATE_INFO, flags = FENCE_CREATE_SIGNALED_BIT };
            ulong created;
            Check(vkCreateFence(_device, &fInfo, IntPtr.Zero, &created), "fence");
            slot.Fence = created;
            _slots[index] = slot;
        }
        var fence = slot.Fence;
        vkWaitForFences(_device, 1, &fence, 1, ulong.MaxValue);
        vkResetFences(_device, 1, &fence);
        vkResetCommandBuffer(slot.Commands, 0);
        var begin = new CommandBufferBeginInfo { sType = STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO, flags = COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT };
        Check(vkBeginCommandBuffer(slot.Commands, &begin), "begin");
        return slot;
    }

    void Submit(Slot slot)
    {
        var cb = slot.Commands;
        var submit = new SubmitInfo { sType = STRUCTURE_TYPE_SUBMIT_INFO, commandBufferCount = 1, pCommandBuffers = &cb };
        Check(vkQueueSubmit(_queue, 1, &submit, slot.Fence), "submit");
    }

    /// <summary>A slot's uniform buffer holds at least <paramref name="size"/> bytes (grows, rarely, between submissions).</summary>
    void EnsureUniforms(Slot slot, ulong size)
    {
        if (slot.Size >= size)
            return;
        if (slot.Buffer != 0)
        {
            vkDestroyBuffer(_device, slot.Buffer, IntPtr.Zero);
            vkFreeMemory(_device, slot.Memory, IntPtr.Zero);
        }
        var grown = Math.Max(size, slot.Size * 2);
        slot.Buffer = HostBuffer(grown, BUFFER_USAGE_UNIFORM_BUFFER_BIT, out var uniformMemory, out var uniformMapped);
        slot.Memory = uniformMemory;
        slot.Mapped = uniformMapped;
        slot.Size = grown;
        if (slot.Set == 0)
        {
            var layout = _setLayoutDraw;
            var alloc = new DescriptorSetAllocateInfo { sType = STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO, descriptorPool = _descriptorPool, descriptorSetCount = 1, pSetLayouts = &layout };
            ulong set;
            Check(vkAllocateDescriptorSets(_device, &alloc, &set), "draw set");
            slot.Set = set;
        }
        var infos = stackalloc DescriptorBufferInfo[2];
        infos[0] = new DescriptorBufferInfo { buffer = slot.Buffer, range = DrawBlockSize };
        infos[1] = new DescriptorBufferInfo { buffer = slot.Buffer, range = JointsBlockSize };
        var writes = stackalloc WriteDescriptorSet[2];
        for (uint i = 0; i < 2; i++)
            writes[i] = new WriteDescriptorSet
            {
                sType = STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET, dstSet = slot.Set, dstBinding = i, descriptorCount = 1,
                descriptorType = DESCRIPTOR_TYPE_UNIFORM_BUFFER_DYNAMIC, pBufferInfo = &infos[i],
            };
        vkUpdateDescriptorSets(_device, 2, writes, 0, IntPtr.Zero);
    }

    /// <summary>The bytes a skin's joint matrices take in the uniform buffer, 256-aligned (the largest offset alignment
    /// Vulkan allows).</summary>
    static uint JointsBytes(int joints) => (uint)((Math.Min(joints, MaxJoints) * 64 + 255) & ~255);

    /// <summary>Upper bound of the uniform bytes a frame of these objects writes: per pass, a draw block per primitive and
    /// the joints of each skinned node; then a whole joints block of slack, the range every joints binding covers.</summary>
    static ulong UniformBytes(RenderItem[] items, int count, int passes)
    {
        ulong perPass = 0;
        for (var o = 0; o < count; o++)
        {
            var scene = items[o].Scene;
            foreach (var node in scene.Nodes)
            {
                if (node.Mesh < 0 || node.Mesh >= scene.Meshes.Length)
                    continue;
                perPass += (ulong)scene.Meshes[node.Mesh].Length * DrawBlockSize;
                if (node.Skin >= 0 && node.Skin < scene.Skins.Length)
                    perPass += JointsBytes(scene.Skins[node.Skin].Joints.Length);
            }
        }
        return JointsBlockSize + perPass * (ulong)passes + JointsBlockSize;
    }

    void Render(VkTarget t, RenderItem[] items, int count, in RenderStyle style, in Matrix4x4 viewProj, int w, int h)
    {
        // Models and images first: an upload submits and waits on its own command buffer.
        for (var o = 0; o < count; o++)
        {
            Model(items[o].Scene);
            foreach (var m in items[o].Materials)
                if (m.Image != null)
                    ImageTexture(m.Image.Pixels);
        }

        var firstPass = style.Outline > 0 ? 0 : 1;
        var slot = NextSlot();
        EnsureUniforms(slot, UniformBytes(items, count, 3 - firstPass));
        var cb = slot.Commands;

        var clears = stackalloc ClearValue[2];
        clears[0] = default;
        clears[1] = new ClearValue { r = 1 };
        var begin = new RenderPassBeginInfo
        {
            sType = STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO, renderPass = _renderPass, framebuffer = t.Framebuffer,
            width = (uint)w, height = (uint)h, clearValueCount = 2, pClearValues = clears,
        };
        vkCmdBeginRenderPass(cb, &begin, SUBPASS_CONTENTS_INLINE);
        var vp = new Viewport { width = w, height = h, maxDepth = 1 };
        vkCmdSetViewport(cb, 0, 1, &vp);
        var scissor = new Rect2D { width = (uint)w, height = (uint)h };
        vkCmdSetScissor(cb, 0, 1, &scissor);

        var frame = new DrawBlock
        {
            ViewProj = viewProj,
            Light = new Vector4(Light, 0),
            OutlineColor = style.OutlineColor,
            Params = new Vector4(0, w, h, 0),
            Look = (int)style.Look,
        };
        // The joints block at 0 stays bound for unskinned draws.
        ulong used = JointsBlockSize;
        var pipeline = -1;
        ulong boundTexture = 0;
        for (var pass = firstPass; pass < 3; pass++)
        {
            var outlinePass = pass == 0;
            var blendPass = pass == 2;
            frame.OutlinePass = outlinePass ? 1 : 0;
            frame.Params.X = outlinePass ? style.Outline : 0;
            for (var o = 0; o < count; o++)
                DrawItem(cb, slot, items[o], outlinePass, blendPass, ref frame, ref used, ref pipeline, ref boundTexture);
        }

        vkCmdEndRenderPass(cb);
        Check(vkEndCommandBuffer(cb), "end");
        Submit(slot);
    }

    void DrawItem(IntPtr cb, Slot slot, in RenderItem item, bool outlinePass, bool blendPass, ref DrawBlock block, ref ulong used,
        ref int pipeline, ref ulong boundTexture)
    {
        var scene = item.Scene;
        var gpu = Model(scene);
        var nodes = scene.Nodes;
        var buffers = stackalloc ulong[6];
        var offsets = stackalloc ulong[6];
        var dynamicOffsets = stackalloc uint[2];
        for (var i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
            if (node.Mesh < 0 || node.Mesh >= gpu.Offsets.Length || item.Hidden[i])
                continue;
            var skinned = node.Skin >= 0 && node.Skin < scene.Skins.Length;
            var primitives = scene.Meshes[node.Mesh];
            uint jointsOffset = 0;
            var jointsSet = false;
            for (var k = 0; k < primitives.Length; k++)
            {
                var p = primitives[k];
                var materialIndex = p.Material;
                var material = materialIndex >= 0 && materialIndex < scene.Materials.Length ? scene.Materials[materialIndex] : null;
                var changed = materialIndex >= 0 && materialIndex < item.Materials.Length ? item.Materials[materialIndex] : default;
                if (changed.Hidden || (material?.Blend ?? false) != blendPass)
                    continue;
                if (!jointsSet)
                {
                    jointsSet = true;
                    block.Model = skinned ? Matrix4x4.Identity : item.Pose[i];
                    if (skinned)
                    {
                        var n = scene.JointMatrices(node.Skin, item.Pose, _joints);
                        jointsOffset = (uint)used;
                        fixed (Matrix4x4* j = _joints)
                            Buffer.MemoryCopy(j, slot.Mapped + used, JointsBlockSize, n * 64);
                        used += JointsBytes(n);
                    }
                }
                block.Color = changed.HasColor ? changed.Color : material?.Color ?? new Vector4(0.8f, 0.8f, 0.8f, 1);
                // An image put on the material from code, else the file's own.
                var texture = changed.Image != null ? ImageTexture(changed.Image.Pixels)
                    : material != null && material.Texture >= 0 && material.Texture < gpu.Textures.Length ? gpu.Textures[material.Texture] : null;
                block.Textured = texture != null ? 1 : 0;
                block.Skinned = skinned && p.Joints.Length > 0 ? 1 : 0;
                var drawOffset = (uint)used;
                *(DrawBlock*)(slot.Mapped + used) = block;
                used += DrawBlockSize;

                var cull = outlinePass ? 2 : (material?.DoubleSided ?? false) ? 0 : 1;
                var want = cull * 2 + (blendPass ? 1 : 0);
                if (pipeline != want)
                {
                    vkCmdBindPipeline(cb, PIPELINE_BIND_POINT_GRAPHICS, _pipelines[want]);
                    pipeline = want;
                }
                dynamicOffsets[0] = drawOffset;
                dynamicOffsets[1] = jointsOffset;
                var set = slot.Set;
                vkCmdBindDescriptorSets(cb, PIPELINE_BIND_POINT_GRAPHICS, _pipelineLayout, 0, 1, &set, 2, dynamicOffsets);
                var textureSet = (texture ?? _white).Set;
                if (boundTexture != textureSet)
                {
                    vkCmdBindDescriptorSets(cb, PIPELINE_BIND_POINT_GRAPHICS, _pipelineLayout, 1, 1, &textureSet, 0, null);
                    boundTexture = textureSet;
                }
                var at = gpu.Offsets[node.Mesh][k];
                for (var a = 0; a < 6; a++)
                {
                    buffers[a] = gpu.Buffer;
                    offsets[a] = at[a];
                }
                vkCmdBindVertexBuffers(cb, 0, 6, buffers, offsets);
                vkCmdBindIndexBuffer(cb, gpu.Buffer, at[6], INDEX_TYPE_UINT32);
                vkCmdDrawIndexed(cb, (uint)gpu.Counts[node.Mesh][k], 1, 0, 0, 0);
            }
        }
    }

    #endregion
}
