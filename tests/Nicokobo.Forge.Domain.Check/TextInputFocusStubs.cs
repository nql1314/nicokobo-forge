// Model Unity's destroyed-object equality while retaining the managed wrapper.
namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed { get; set; }
        public static bool operator ==(Object? left, Object? right)
        {
            bool leftNull = ReferenceEquals(left, null) || left.Destroyed;
            bool rightNull = ReferenceEquals(right, null) || right.Destroyed;
            return leftNull || rightNull ? leftNull == rightNull : ReferenceEquals(left, right);
        }
        public static bool operator !=(Object? left, Object? right) => !(left == right);
        public override bool Equals(object? other) => ReferenceEquals(this, other);
        public override int GetHashCode() => base.GetHashCode();
        protected void RequireAlive()
        {
            if (Destroyed) throw new NullReferenceException("Native Unity object was destroyed");
        }
    }
    public class Component : Object
    {
        public T? TryCast<T>() where T : class => this as T;
        public bool isActiveAndEnabled { get; set; } = true;
    }
    public class GameObject : Object
    {
        public Component? Field { get; set; }
        public Component? GetComponentInParent(Type type)
        {
            RequireAlive();
            return Field != null && type.IsInstanceOfType(Field) ? Field : null;
        }
    }
    public static class Application { public static bool isFocused { get; set; } = true; }
    public static class Input { public static string compositionString { get; set; } = ""; }
}
namespace UnityEngine.EventSystems
{
    public class EventSystem : UnityEngine.Object
    {
        public static EventSystem? current { get; set; }
        public UnityEngine.GameObject? Selected { get; set; }
        public UnityEngine.GameObject? currentSelectedGameObject
        {
            get { RequireAlive(); return Selected; }
        }
    }
}
namespace Il2CppTMPro
{
    public class TMP_InputField : UnityEngine.Component { public bool isFocused { get; set; } }
}
namespace UnityEngine.UI
{
    public class InputField : UnityEngine.Component { public bool isFocused { get; set; } }
}
namespace Il2CppInterop.Runtime
{
    public static class Il2CppType { public static Type Of<T>() => typeof(T); }
}
