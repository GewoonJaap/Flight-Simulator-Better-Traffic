using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Simvars
{
    public class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string _sPropertyName = null)
        {
            var hEventHandler = PropertyChanged;
            if (hEventHandler != null && !string.IsNullOrEmpty(_sPropertyName))
                hEventHandler(this, new PropertyChangedEventArgs(_sPropertyName));
        }

        protected bool SetProperty<T>(ref T _tField, T _tValue, [CallerMemberName] string _sPropertyName = null)
        {
            return SetProperty(ref _tField, _tValue, out var tPreviousValue, _sPropertyName);
        }

        protected bool SetProperty<T>(ref T _tField, T _tValue, out T _tPreviousValue,
            [CallerMemberName] string _sPropertyName = null)
        {
            if (!Equals(_tField, _tValue))
            {
                _tPreviousValue = _tField;
                _tField = _tValue;
                OnPropertyChanged(_sPropertyName);
                return true;
            }

            _tPreviousValue = default;
            return false;
        }
    }

    public class BaseViewModel : ObservableObject
    {
    }

    public class BaseCommand : ICommand
    {
        public BaseCommand()
        {
            ExecuteDelegate = null;
        }

        public BaseCommand(Action<object> _ExecuteDelegate)
        {
            ExecuteDelegate = _ExecuteDelegate;
        }

        public Action<object> ExecuteDelegate { get; set; }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object _oParameter)
        {
            return true;
        }

        public void Execute(object _oParameter)
        {
            ExecuteDelegate?.Invoke(_oParameter);
        }
    }
}