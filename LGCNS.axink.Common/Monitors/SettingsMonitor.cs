using CommunityToolkit.Mvvm.Messaging;

namespace LGCNS.axink.Common.Monitors
{
    public interface ISettingsMonitor<T> where T : class, new()
    {
        T Current { get; }
        event EventHandler<T>? Changed;
        void Reload();
        void UpdateAndSave(T newValue);
    }


    public sealed class SettingsMonitor<T> : ISettingsMonitor<T> where T : class, new()
    {
        private readonly JsonFileStore<T> _store;
        private readonly IMessenger _messenger;

        public T Current { get; private set; }

        public event EventHandler<T>? Changed;

        public SettingsMonitor(JsonFileStore<T> store, IMessenger messenger)
        {
            _store = store;
            _messenger = messenger;
            Current = _store.LoadOrCreate();
        }

        public void Reload()
        {
            Current = _store.LoadOrCreate();
            Changed?.Invoke(this, Current);
            _messenger.Send(new SettingsChangedMessage<T>(Current));
        }

        public void UpdateAndSave(T newValue)
        {
            _store.Save(newValue);
            Current = newValue;

            Changed?.Invoke(this, Current);
            _messenger.Send(new SettingsChangedMessage<T>(Current));
        }
    }
}
