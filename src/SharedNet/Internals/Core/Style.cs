using Newtonsoft.Json.Serialization;

namespace DrawnUi.Draw
{
    public class Style
    {
        public Style()
        {
            Behaviors = new List<Behavior>();
            Setters = new List<Setter>();
            Triggers = new List<TriggerBase>();
        }
        /// <summary>Gets or sets a Boolean value that controls whether the style should be applied to controls that are derived from the base type.</summary>
        public bool ApplyToDerivedTypes { get; set; }

        public Style BasedOn { get; set; }

        public string BaseResourceKey { get; set; }

        public IList<Behavior> Behaviors { get; }

        public bool CanCascade { get; set; }

        public string Class { get; set; }

        public IList<Setter> Setters { get; }

        public IList<TriggerBase> Triggers { get; }

        public Type TargetType { get; set; }
    }

    public abstract class TriggerBase : BindableObject, IAttachedObject
    {
        /// <summary>Gets the list of <see cref="T:Microsoft.Maui.Controls.TriggerAction" /> objects that will be invoked when the trigger condition is met. Ignored for the <see cref="T:Microsoft.Maui.Controls.EventTrigger" /> class.</summary>
        /// <value>
        /// </value>
        public IList<TriggerAction> EnterActions { get; }
        /// <summary>Gets the list of <see cref="T:Microsoft.Maui.Controls.TriggerAction" /> objects that will be invoked after the trigger condition is no longer met. Ignored for the <see cref="T:Microsoft.Maui.Controls.EventTrigger" /> class.</summary>
        /// <value>
        /// </value>
        public IList<TriggerAction> ExitActions { get; }
        /// <summary>Gets a value that indicates whether or not the trigger is sealed.</summary>
        /// <remarks>A trigger becomes sealed when its `IAttachedObject.AttachTo(Microsoft.Maui.Controls.BindableObject)` method is called. Once it is sealed, its <see cref="P:Microsoft.Maui.Controls.TriggerBase.EnterActions" /> and <see cref="P:Microsoft.Maui.Controls.TriggerBase.ExitActions" /> lists become readonly.</remarks>
        public bool IsSealed { get; private set; }
        /// <summary>The type of object to which this <see cref="T:Microsoft.Maui.Controls.TriggerBase" /> object can be attached.</summary>
        public Type TargetType { get; }

        public void AttachTo(BindableObject bindable)
        {
 
        }

        public void DetachFrom(BindableObject bindable)
        {
 
        }
    }

    public abstract class TriggerAction
    {
        protected Type AssociatedType { get; private set; }
        protected abstract void Invoke(object sender);
    }

    public abstract class Behavior : BindableObject, IAttachedObject
    {


        /// <summary>
        /// Gets the type of the objects with which this <see cref="T:Microsoft.Maui.Controls.Behavior" /> can be associated.
        /// </summary>
        protected Type AssociatedType { get; }

        /// <summary>
        /// Application developers override this method to implement the behaviors that will be associated with <paramref name="bindable" />.
        /// </summary>
        /// <param name="bindable">The bindable object to which the behavior was attached.</param>
        protected virtual void OnAttachedTo(BindableObject bindable)
        {

        }

        /// <summary>
        /// Application developers override this method to remove the behaviors from <paramref name="bindable" />
        /// that were implemented in a previous call to the <see cref="M:Microsoft.Maui.Controls.Behavior.OnAttachedTo(Microsoft.Maui.Controls.BindableObject)" /> method.
        /// </summary>
        /// <param name="bindable">The bindable object from which the behavior was detached.</param>
        protected virtual void OnDetachingFrom(BindableObject bindable)
        {

        }

        public void AttachTo(BindableObject bindable)
        {
            OnAttachedTo(bindable);
        }

        public void DetachFrom(BindableObject bindable)
        {
            OnDetachingFrom(bindable);
        }
    }

    public interface IAttachedObject
    {
        void AttachTo(BindableObject bindable);
        void DetachFrom(BindableObject bindable);
    }


    [ContentProperty("Value")]
    public class Setter : IValueProvider
    {
        public string TargetName { get; set; }
        /// <summary>The property on which to apply the assignment.</summary>
        /// <remarks>
        /// <para>Only bindable properties can be set with a <see cref="T:Microsoft.Maui.Controls.Setter" />.</para>.</remarks>
        public BindableProperty Property { get; set; }
        /// <summary>The value to assign to the property.</summary>
        public object Value { get; set; }

        public void SetValue(object target, object? value)
        {
            //TODO


        }

        public object? GetValue(object target)
        {
            //TODO
            return null;
        }
    }
}
