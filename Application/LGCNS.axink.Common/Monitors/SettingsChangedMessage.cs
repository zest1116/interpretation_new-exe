using CommunityToolkit.Mvvm.Messaging.Messages;

namespace LGCNS.axink.Common.Monitors
{
    public sealed class SettingsChangedMessage<T> : ValueChangedMessage<T>
    {
        public SettingsChangedMessage(T value) : base(value) { }
    }
}
