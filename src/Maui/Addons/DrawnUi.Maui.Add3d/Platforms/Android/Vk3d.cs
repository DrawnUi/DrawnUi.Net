using System.Runtime.InteropServices;

namespace DrawnUi.Add3d;

/// <summary>
/// The Vulkan 1.0 calls and structures the 3D renderer needs (<see cref="VulkanRenderer3D"/>), bound to the system
/// loader. Dispatchable handles (device, queue, command buffer) are pointers, the others 64-bit; the structures follow
/// the C layout on 64-bit Android.
/// </summary>
internal static unsafe class Vk3d
{
    const string Lib = "libvulkan.so";

    public const int SUCCESS = 0;

    public const uint STRUCTURE_TYPE_SUBMIT_INFO = 4;
    public const uint STRUCTURE_TYPE_MEMORY_ALLOCATE_INFO = 5;
    public const uint STRUCTURE_TYPE_FENCE_CREATE_INFO = 8;
    public const uint STRUCTURE_TYPE_BUFFER_CREATE_INFO = 12;
    public const uint STRUCTURE_TYPE_IMAGE_CREATE_INFO = 14;
    public const uint STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO = 15;
    public const uint STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO = 16;
    public const uint STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO = 18;
    public const uint STRUCTURE_TYPE_PIPELINE_VERTEX_INPUT_STATE_CREATE_INFO = 19;
    public const uint STRUCTURE_TYPE_PIPELINE_INPUT_ASSEMBLY_STATE_CREATE_INFO = 20;
    public const uint STRUCTURE_TYPE_PIPELINE_VIEWPORT_STATE_CREATE_INFO = 22;
    public const uint STRUCTURE_TYPE_PIPELINE_RASTERIZATION_STATE_CREATE_INFO = 23;
    public const uint STRUCTURE_TYPE_PIPELINE_MULTISAMPLE_STATE_CREATE_INFO = 24;
    public const uint STRUCTURE_TYPE_PIPELINE_DEPTH_STENCIL_STATE_CREATE_INFO = 25;
    public const uint STRUCTURE_TYPE_PIPELINE_COLOR_BLEND_STATE_CREATE_INFO = 26;
    public const uint STRUCTURE_TYPE_PIPELINE_DYNAMIC_STATE_CREATE_INFO = 27;
    public const uint STRUCTURE_TYPE_GRAPHICS_PIPELINE_CREATE_INFO = 28;
    public const uint STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO = 30;
    public const uint STRUCTURE_TYPE_SAMPLER_CREATE_INFO = 31;
    public const uint STRUCTURE_TYPE_DESCRIPTOR_SET_LAYOUT_CREATE_INFO = 32;
    public const uint STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO = 33;
    public const uint STRUCTURE_TYPE_DESCRIPTOR_SET_ALLOCATE_INFO = 34;
    public const uint STRUCTURE_TYPE_WRITE_DESCRIPTOR_SET = 35;
    public const uint STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO = 37;
    public const uint STRUCTURE_TYPE_RENDER_PASS_CREATE_INFO = 38;
    public const uint STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO = 39;
    public const uint STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO = 40;
    public const uint STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO = 42;
    public const uint STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO = 43;
    public const uint STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER = 45;

    public const uint FORMAT_R8G8B8A8_UNORM = 37;
    public const uint FORMAT_R32G32_SFLOAT = 103;
    public const uint FORMAT_R32G32B32_SFLOAT = 106;
    public const uint FORMAT_R32G32B32A32_SFLOAT = 109;
    public const uint FORMAT_D16_UNORM = 124;
    public const uint FORMAT_X8_D24_UNORM_PACK32 = 125;
    public const uint FORMAT_D32_SFLOAT = 126;

    public const uint FORMAT_FEATURE_DEPTH_STENCIL_ATTACHMENT_BIT = 0x200;

    public const uint IMAGE_USAGE_TRANSFER_SRC_BIT = 0x1;
    public const uint IMAGE_USAGE_TRANSFER_DST_BIT = 0x2;
    public const uint IMAGE_USAGE_SAMPLED_BIT = 0x4;
    public const uint IMAGE_USAGE_COLOR_ATTACHMENT_BIT = 0x10;
    public const uint IMAGE_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT = 0x20;

    public const uint BUFFER_USAGE_TRANSFER_SRC_BIT = 0x1;
    public const uint BUFFER_USAGE_UNIFORM_BUFFER_BIT = 0x10;
    public const uint BUFFER_USAGE_INDEX_BUFFER_BIT = 0x40;
    public const uint BUFFER_USAGE_VERTEX_BUFFER_BIT = 0x80;

    public const uint MEMORY_PROPERTY_DEVICE_LOCAL_BIT = 0x1;
    public const uint MEMORY_PROPERTY_HOST_VISIBLE_BIT = 0x2;
    public const uint MEMORY_PROPERTY_HOST_COHERENT_BIT = 0x4;

    public const uint IMAGE_LAYOUT_UNDEFINED = 0;
    public const uint IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL = 2;
    public const uint IMAGE_LAYOUT_DEPTH_STENCIL_ATTACHMENT_OPTIMAL = 3;
    public const uint IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL = 5;
    public const uint IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL = 6;
    public const uint IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL = 7;

    public const uint IMAGE_ASPECT_COLOR_BIT = 0x1;
    public const uint IMAGE_ASPECT_DEPTH_BIT = 0x2;
    public const uint QUEUE_FAMILY_IGNORED = ~0u;

    public const uint PIPELINE_STAGE_TOP_OF_PIPE_BIT = 0x1;
    public const uint PIPELINE_STAGE_EARLY_FRAGMENT_TESTS_BIT = 0x100;
    public const uint PIPELINE_STAGE_LATE_FRAGMENT_TESTS_BIT = 0x200;
    public const uint PIPELINE_STAGE_FRAGMENT_SHADER_BIT = 0x80;
    public const uint PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT = 0x400;
    public const uint PIPELINE_STAGE_TRANSFER_BIT = 0x1000;

    public const uint ACCESS_SHADER_READ_BIT = 0x20;
    public const uint ACCESS_COLOR_ATTACHMENT_READ_BIT = 0x80;
    public const uint ACCESS_COLOR_ATTACHMENT_WRITE_BIT = 0x100;
    public const uint ACCESS_DEPTH_STENCIL_ATTACHMENT_READ_BIT = 0x200;
    public const uint ACCESS_DEPTH_STENCIL_ATTACHMENT_WRITE_BIT = 0x400;
    public const uint ACCESS_TRANSFER_READ_BIT = 0x800;
    public const uint ACCESS_TRANSFER_WRITE_BIT = 0x1000;

    public const uint SUBPASS_EXTERNAL = ~0u;

    public const uint FENCE_CREATE_SIGNALED_BIT = 0x1;
    public const uint COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT = 0x2;
    public const uint COMMAND_BUFFER_USAGE_ONE_TIME_SUBMIT_BIT = 0x1;

    public const uint DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER = 1;
    public const uint DESCRIPTOR_TYPE_UNIFORM_BUFFER_DYNAMIC = 8;
    public const uint DESCRIPTOR_POOL_CREATE_FREE_DESCRIPTOR_SET_BIT = 0x1;

    public const uint SHADER_STAGE_VERTEX_BIT = 0x1;
    public const uint SHADER_STAGE_FRAGMENT_BIT = 0x10;

    public const uint CULL_MODE_NONE = 0, CULL_MODE_FRONT_BIT = 1, CULL_MODE_BACK_BIT = 2;
    public const uint FRONT_FACE_CLOCKWISE = 1;
    public const uint COMPARE_OP_LESS = 1;
    public const uint BLEND_FACTOR_ONE = 1, BLEND_FACTOR_ONE_MINUS_SRC_ALPHA = 7;
    public const uint BLEND_OP_ADD = 0;
    public const uint COLOR_COMPONENT_ALL = 0xF;
    public const uint DYNAMIC_STATE_VIEWPORT = 0, DYNAMIC_STATE_SCISSOR = 1;
    public const uint PRIMITIVE_TOPOLOGY_TRIANGLE_LIST = 3;
    public const uint INDEX_TYPE_UINT32 = 1;
    public const uint FILTER_LINEAR = 1;
    public const uint SAMPLER_MIPMAP_MODE_LINEAR = 1;
    public const uint SAMPLER_ADDRESS_MODE_REPEAT = 0;
    public const uint ATTACHMENT_LOAD_OP_CLEAR = 1, ATTACHMENT_LOAD_OP_DONT_CARE = 2;
    public const uint ATTACHMENT_STORE_OP_STORE = 0, ATTACHMENT_STORE_OP_DONT_CARE = 1;
    public const uint SUBPASS_CONTENTS_INLINE = 0;
    public const uint PIPELINE_BIND_POINT_GRAPHICS = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct MemoryRequirements { public ulong size, alignment; public uint memoryTypeBits; }

    [StructLayout(LayoutKind.Sequential)]
    public struct FormatProperties { public uint linearTilingFeatures, optimalTilingFeatures, bufferFeatures; }

    [StructLayout(LayoutKind.Sequential)]
    public struct Extent3D { public uint width, height, depth; }

    [StructLayout(LayoutKind.Sequential)]
    public struct Offset3D { public int x, y, z; }

    [StructLayout(LayoutKind.Sequential)]
    public struct ImageSubresourceRange { public uint aspectMask, baseMipLevel, levelCount, baseArrayLayer, layerCount; }

    [StructLayout(LayoutKind.Sequential)]
    public struct ImageSubresourceLayers { public uint aspectMask, mipLevel, baseArrayLayer, layerCount; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MemoryAllocateInfo { public uint sType; public IntPtr pNext; public ulong allocationSize; public uint memoryTypeIndex; }

    [StructLayout(LayoutKind.Sequential)]
    public struct BufferCreateInfo
    {
        public uint sType; public IntPtr pNext; public uint flags; public ulong size; public uint usage, sharingMode, queueFamilyIndexCount;
        public uint* pQueueFamilyIndices;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ImageCreateInfo
    {
        public uint sType; public IntPtr pNext; public uint flags, imageType, format; public Extent3D extent;
        public uint mipLevels, arrayLayers, samples, tiling, usage, sharingMode, queueFamilyIndexCount; public uint* pQueueFamilyIndices;
        public uint initialLayout;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ImageViewCreateInfo
    {
        public uint sType; public IntPtr pNext; public uint flags; public ulong image; public uint viewType, format;
        public uint r, g, b, a; public ImageSubresourceRange subresourceRange;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SamplerCreateInfo
    {
        public uint sType; public IntPtr pNext; public uint flags, magFilter, minFilter, mipmapMode, addressModeU, addressModeV, addressModeW;
        public float mipLodBias; public uint anisotropyEnable; public float maxAnisotropy; public uint compareEnable, compareOp;
        public float minLod, maxLod; public uint borderColor, unnormalizedCoordinates;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AttachmentDescription
    {
        public uint flags, format, samples, loadOp, storeOp, stencilLoadOp, stencilStoreOp, initialLayout, finalLayout;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AttachmentReference { public uint attachment, layout; }

    [StructLayout(LayoutKind.Sequential)]
    public struct SubpassDescription
    {
        public uint flags, pipelineBindPoint, inputAttachmentCount; public AttachmentReference* pInputAttachments;
        public uint colorAttachmentCount; public AttachmentReference* pColorAttachments, pResolveAttachments, pDepthStencilAttachment;
        public uint preserveAttachmentCount; public uint* pPreserveAttachments;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SubpassDependency { public uint srcSubpass, dstSubpass, srcStageMask, dstStageMask, srcAccessMask, dstAccessMask, dependencyFlags; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RenderPassCreateInfo
    {
        public uint sType; public IntPtr pNext; public uint flags, attachmentCount; public AttachmentDescription* pAttachments;
        public uint subpassCount; public SubpassDescription* pSubpasses; public uint dependencyCount; public SubpassDependency* pDependencies;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FramebufferCreateInfo
    {
        public uint sType; public IntPtr pNext; public uint flags; public ulong renderPass; public uint attachmentCount; public ulong* pAttachments;
        public uint width, height, layers;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ShaderModuleCreateInfo { public uint sType; public IntPtr pNext; public uint flags; public nuint codeSize; public uint* pCode; }

    [StructLayout(LayoutKind.Sequential)]
    public struct DescriptorSetLayoutBinding { public uint binding, descriptorType, descriptorCount, stageFlags; public ulong* pImmutableSamplers; }

    [StructLayout(LayoutKind.Sequential)]
    public struct DescriptorSetLayoutCreateInfo { public uint sType; public IntPtr pNext; public uint flags, bindingCount; public DescriptorSetLayoutBinding* pBindings; }

    [StructLayout(LayoutKind.Sequential)]
    public struct PipelineLayoutCreateInfo
    {
        public uint sType; public IntPtr pNext; public uint flags, setLayoutCount; public ulong* pSetLayouts;
        public uint pushConstantRangeCount; public IntPtr pPushConstantRanges;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DescriptorPoolSize { public uint type, descriptorCount; }

    [StructLayout(LayoutKind.Sequential)]
    public struct DescriptorPoolCreateInfo { public uint sType; public IntPtr pNext; public uint flags, maxSets, poolSizeCount; public DescriptorPoolSize* pPoolSizes; }

    [StructLayout(LayoutKind.Sequential)]
    public struct DescriptorSetAllocateInfo { public uint sType; public IntPtr pNext; public ulong descriptorPool; public uint descriptorSetCount; public ulong* pSetLayouts; }

    [StructLayout(LayoutKind.Sequential)]
    public struct DescriptorBufferInfo { public ulong buffer, offset, range; }

    [StructLayout(LayoutKind.Sequential)]
    public struct DescriptorImageInfo { public ulong sampler, imageView; public uint imageLayout; }

    [StructLayout(LayoutKind.Sequential)]
    public struct WriteDescriptorSet
    {
        public uint sType; public IntPtr pNext; public ulong dstSet; public uint dstBinding, dstArrayElement, descriptorCount, descriptorType;
        public DescriptorImageInfo* pImageInfo; public DescriptorBufferInfo* pBufferInfo; public ulong* pTexelBufferView;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PipelineShaderStageCreateInfo
    {
        public uint sType; public IntPtr pNext; public uint flags, stage; public ulong module; public byte* pName; public IntPtr pSpecializationInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VertexInputBindingDescription { public uint binding, stride, inputRate; }

    [StructLayout(LayoutKind.Sequential)]
    public struct VertexInputAttributeDescription { public uint location, binding, format, offset; }

    [StructLayout(LayoutKind.Sequential)]
    public struct PipelineVertexInputStateCreateInfo
    {
        public uint sType; public IntPtr pNext; public uint flags, vertexBindingDescriptionCount; public VertexInputBindingDescription* pVertexBindingDescriptions;
        public uint vertexAttributeDescriptionCount; public VertexInputAttributeDescription* pVertexAttributeDescriptions;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PipelineInputAssemblyStateCreateInfo { public uint sType; public IntPtr pNext; public uint flags, topology, primitiveRestartEnable; }

    [StructLayout(LayoutKind.Sequential)]
    public struct PipelineViewportStateCreateInfo
    {
        public uint sType; public IntPtr pNext; public uint flags, viewportCount; public IntPtr pViewports; public uint scissorCount; public IntPtr pScissors;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PipelineRasterizationStateCreateInfo
    {
        public uint sType; public IntPtr pNext; public uint flags, depthClampEnable, rasterizerDiscardEnable, polygonMode, cullMode, frontFace, depthBiasEnable;
        public float depthBiasConstantFactor, depthBiasClamp, depthBiasSlopeFactor, lineWidth;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PipelineMultisampleStateCreateInfo
    {
        public uint sType; public IntPtr pNext; public uint flags, rasterizationSamples, sampleShadingEnable; public float minSampleShading;
        public uint* pSampleMask; public uint alphaToCoverageEnable, alphaToOneEnable;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct StencilOpState { public uint failOp, passOp, depthFailOp, compareOp, compareMask, writeMask, reference; }

    [StructLayout(LayoutKind.Sequential)]
    public struct PipelineDepthStencilStateCreateInfo
    {
        public uint sType; public IntPtr pNext; public uint flags, depthTestEnable, depthWriteEnable, depthCompareOp, depthBoundsTestEnable, stencilTestEnable;
        public StencilOpState front, back; public float minDepthBounds, maxDepthBounds;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PipelineColorBlendAttachmentState
    {
        public uint blendEnable, srcColorBlendFactor, dstColorBlendFactor, colorBlendOp, srcAlphaBlendFactor, dstAlphaBlendFactor, alphaBlendOp, colorWriteMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PipelineColorBlendStateCreateInfo
    {
        public uint sType; public IntPtr pNext; public uint flags, logicOpEnable, logicOp, attachmentCount; public PipelineColorBlendAttachmentState* pAttachments;
        public float c0, c1, c2, c3;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PipelineDynamicStateCreateInfo { public uint sType; public IntPtr pNext; public uint flags, dynamicStateCount; public uint* pDynamicStates; }

    [StructLayout(LayoutKind.Sequential)]
    public struct GraphicsPipelineCreateInfo
    {
        public uint sType; public IntPtr pNext; public uint flags, stageCount; public PipelineShaderStageCreateInfo* pStages;
        public PipelineVertexInputStateCreateInfo* pVertexInputState; public PipelineInputAssemblyStateCreateInfo* pInputAssemblyState;
        public IntPtr pTessellationState; public PipelineViewportStateCreateInfo* pViewportState;
        public PipelineRasterizationStateCreateInfo* pRasterizationState; public PipelineMultisampleStateCreateInfo* pMultisampleState;
        public PipelineDepthStencilStateCreateInfo* pDepthStencilState; public PipelineColorBlendStateCreateInfo* pColorBlendState;
        public PipelineDynamicStateCreateInfo* pDynamicState; public ulong layout, renderPass; public uint subpass; public ulong basePipelineHandle;
        public int basePipelineIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CommandPoolCreateInfo { public uint sType; public IntPtr pNext; public uint flags, queueFamilyIndex; }

    [StructLayout(LayoutKind.Sequential)]
    public struct CommandBufferAllocateInfo { public uint sType; public IntPtr pNext; public ulong commandPool; public uint level, commandBufferCount; }

    [StructLayout(LayoutKind.Sequential)]
    public struct CommandBufferBeginInfo { public uint sType; public IntPtr pNext; public uint flags; public IntPtr pInheritanceInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct FenceCreateInfo { public uint sType; public IntPtr pNext; public uint flags; }

    [StructLayout(LayoutKind.Sequential)]
    public struct SubmitInfo
    {
        public uint sType; public IntPtr pNext; public uint waitSemaphoreCount; public ulong* pWaitSemaphores; public uint* pWaitDstStageMask;
        public uint commandBufferCount; public IntPtr* pCommandBuffers; public uint signalSemaphoreCount; public ulong* pSignalSemaphores;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ClearValue { public float r, g, b, a; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RenderPassBeginInfo
    {
        public uint sType; public IntPtr pNext; public ulong renderPass, framebuffer; public int x, y; public uint width, height;
        public uint clearValueCount; public ClearValue* pClearValues;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Viewport { public float x, y, width, height, minDepth, maxDepth; }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect2D { public int x, y; public uint width, height; }

    [StructLayout(LayoutKind.Sequential)]
    public struct ImageMemoryBarrier
    {
        public uint sType; public IntPtr pNext; public uint srcAccessMask, dstAccessMask, oldLayout, newLayout, srcQueueFamilyIndex, dstQueueFamilyIndex;
        public ulong image; public ImageSubresourceRange subresourceRange;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BufferImageCopy
    {
        public ulong bufferOffset; public uint bufferRowLength, bufferImageHeight; public ImageSubresourceLayers imageSubresource;
        public Offset3D imageOffset; public Extent3D imageExtent;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ImageBlit
    {
        public ImageSubresourceLayers srcSubresource; public Offset3D src0, src1; public ImageSubresourceLayers dstSubresource; public Offset3D dst0, dst1;
    }

    [DllImport(Lib)] public static extern void vkGetPhysicalDeviceMemoryProperties(IntPtr physicalDevice, byte* properties);
    [DllImport(Lib)] public static extern void vkGetPhysicalDeviceFormatProperties(IntPtr physicalDevice, uint format, FormatProperties* properties);
    [DllImport(Lib)] public static extern int vkDeviceWaitIdle(IntPtr device);
    [DllImport(Lib)] public static extern int vkQueueWaitIdle(IntPtr queue);

    [DllImport(Lib)] public static extern int vkAllocateMemory(IntPtr device, MemoryAllocateInfo* info, IntPtr allocator, ulong* memory);
    [DllImport(Lib)] public static extern void vkFreeMemory(IntPtr device, ulong memory, IntPtr allocator);
    [DllImport(Lib)] public static extern int vkMapMemory(IntPtr device, ulong memory, ulong offset, ulong size, uint flags, void** data);
    [DllImport(Lib)] public static extern int vkCreateBuffer(IntPtr device, BufferCreateInfo* info, IntPtr allocator, ulong* buffer);
    [DllImport(Lib)] public static extern void vkDestroyBuffer(IntPtr device, ulong buffer, IntPtr allocator);
    [DllImport(Lib)] public static extern void vkGetBufferMemoryRequirements(IntPtr device, ulong buffer, MemoryRequirements* requirements);
    [DllImport(Lib)] public static extern int vkBindBufferMemory(IntPtr device, ulong buffer, ulong memory, ulong offset);
    [DllImport(Lib)] public static extern int vkCreateImage(IntPtr device, ImageCreateInfo* info, IntPtr allocator, ulong* image);
    [DllImport(Lib)] public static extern void vkDestroyImage(IntPtr device, ulong image, IntPtr allocator);
    [DllImport(Lib)] public static extern void vkGetImageMemoryRequirements(IntPtr device, ulong image, MemoryRequirements* requirements);
    [DllImport(Lib)] public static extern int vkBindImageMemory(IntPtr device, ulong image, ulong memory, ulong offset);
    [DllImport(Lib)] public static extern int vkCreateImageView(IntPtr device, ImageViewCreateInfo* info, IntPtr allocator, ulong* view);
    [DllImport(Lib)] public static extern void vkDestroyImageView(IntPtr device, ulong view, IntPtr allocator);
    [DllImport(Lib)] public static extern int vkCreateSampler(IntPtr device, SamplerCreateInfo* info, IntPtr allocator, ulong* sampler);

    [DllImport(Lib)] public static extern int vkCreateRenderPass(IntPtr device, RenderPassCreateInfo* info, IntPtr allocator, ulong* renderPass);
    [DllImport(Lib)] public static extern int vkCreateFramebuffer(IntPtr device, FramebufferCreateInfo* info, IntPtr allocator, ulong* framebuffer);
    [DllImport(Lib)] public static extern void vkDestroyFramebuffer(IntPtr device, ulong framebuffer, IntPtr allocator);
    [DllImport(Lib)] public static extern int vkCreateShaderModule(IntPtr device, ShaderModuleCreateInfo* info, IntPtr allocator, ulong* module);
    [DllImport(Lib)] public static extern int vkCreateDescriptorSetLayout(IntPtr device, DescriptorSetLayoutCreateInfo* info, IntPtr allocator, ulong* layout);
    [DllImport(Lib)] public static extern int vkCreatePipelineLayout(IntPtr device, PipelineLayoutCreateInfo* info, IntPtr allocator, ulong* layout);
    [DllImport(Lib)] public static extern int vkCreateGraphicsPipelines(IntPtr device, ulong cache, uint count, GraphicsPipelineCreateInfo* infos, IntPtr allocator, ulong* pipelines);
    [DllImport(Lib)] public static extern int vkCreateDescriptorPool(IntPtr device, DescriptorPoolCreateInfo* info, IntPtr allocator, ulong* pool);
    [DllImport(Lib)] public static extern int vkAllocateDescriptorSets(IntPtr device, DescriptorSetAllocateInfo* info, ulong* sets);
    [DllImport(Lib)] public static extern void vkUpdateDescriptorSets(IntPtr device, uint writeCount, WriteDescriptorSet* writes, uint copyCount, IntPtr copies);

    [DllImport(Lib)] public static extern int vkCreateCommandPool(IntPtr device, CommandPoolCreateInfo* info, IntPtr allocator, ulong* pool);
    [DllImport(Lib)] public static extern int vkAllocateCommandBuffers(IntPtr device, CommandBufferAllocateInfo* info, IntPtr* buffers);
    [DllImport(Lib)] public static extern int vkResetCommandBuffer(IntPtr commandBuffer, uint flags);
    [DllImport(Lib)] public static extern int vkBeginCommandBuffer(IntPtr commandBuffer, CommandBufferBeginInfo* info);
    [DllImport(Lib)] public static extern int vkEndCommandBuffer(IntPtr commandBuffer);
    [DllImport(Lib)] public static extern int vkCreateFence(IntPtr device, FenceCreateInfo* info, IntPtr allocator, ulong* fence);
    [DllImport(Lib)] public static extern int vkWaitForFences(IntPtr device, uint count, ulong* fences, uint waitAll, ulong timeout);
    [DllImport(Lib)] public static extern int vkResetFences(IntPtr device, uint count, ulong* fences);
    [DllImport(Lib)] public static extern int vkQueueSubmit(IntPtr queue, uint count, SubmitInfo* submits, ulong fence);

    [DllImport(Lib)] public static extern void vkCmdBeginRenderPass(IntPtr cb, RenderPassBeginInfo* info, uint contents);
    [DllImport(Lib)] public static extern void vkCmdEndRenderPass(IntPtr cb);
    [DllImport(Lib)] public static extern void vkCmdBindPipeline(IntPtr cb, uint bindPoint, ulong pipeline);
    [DllImport(Lib)] public static extern void vkCmdSetViewport(IntPtr cb, uint first, uint count, Viewport* viewports);
    [DllImport(Lib)] public static extern void vkCmdSetScissor(IntPtr cb, uint first, uint count, Rect2D* scissors);
    [DllImport(Lib)] public static extern void vkCmdBindDescriptorSets(IntPtr cb, uint bindPoint, ulong layout, uint firstSet, uint count, ulong* sets, uint dynamicOffsetCount, uint* dynamicOffsets);
    [DllImport(Lib)] public static extern void vkCmdBindVertexBuffers(IntPtr cb, uint firstBinding, uint count, ulong* buffers, ulong* offsets);
    [DllImport(Lib)] public static extern void vkCmdBindIndexBuffer(IntPtr cb, ulong buffer, ulong offset, uint indexType);
    [DllImport(Lib)] public static extern void vkCmdDrawIndexed(IntPtr cb, uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance);
    [DllImport(Lib)] public static extern void vkCmdPipelineBarrier(IntPtr cb, uint srcStageMask, uint dstStageMask, uint dependencyFlags,
        uint memoryBarrierCount, IntPtr memoryBarriers, uint bufferBarrierCount, IntPtr bufferBarriers, uint imageBarrierCount, ImageMemoryBarrier* imageBarriers);
    [DllImport(Lib)] public static extern void vkCmdCopyBufferToImage(IntPtr cb, ulong buffer, ulong image, uint layout, uint count, BufferImageCopy* regions);
    [DllImport(Lib)] public static extern void vkCmdBlitImage(IntPtr cb, ulong src, uint srcLayout, ulong dst, uint dstLayout, uint count, ImageBlit* regions, uint filter);
}
