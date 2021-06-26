using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace Simvars
{
    public class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string _sPropertyName = null)
        {
            PropertyChangedEventHandler hEventHandler = this.PropertyChanged;
            if (hEventHandler != null && !string.IsNullOrEmpty(_sPropertyName))
            {
                hEventHandler(this, new PropertyChangedEventArgs(_sPropertyName));
            }
        }

        protected bool SetProperty<T>(ref T _tField, T _tValue, [CallerMemberName] string _sPropertyName = null)
        {
            return this.SetProperty(ref _tField, _tValue, out T tPreviousValue, _sPropertyName);
        }

        protected bool SetProperty<T>(ref T _tField, T _tValue, out T _tPreviousValue, [CallerMemberName] string _sPropertyName = null)
        {
            if (!object.Equals(_tField, _tValue))
            {
                _tPreviousValue = _tField;
                _tField = _tValue;
                this.OnPropertyChanged(_sPropertyName);
                return true;
            }

            _tPreviousValue = default(T);
            return false;
        }
    }

    public class BaseViewModel : ObservableObject
    {
    }

    public class BaseCommand : ICommand
    {
        public Action<object> ExecuteDelegate { get; set; }
        public event EventHandler CanExecuteChanged = null;

        public BaseCommand()
        {
            ExecuteDelegate = null;
        }

        public BaseCommand(Action<object> _ExecuteDelegate)
        {
            ExecuteDelegate = _ExecuteDelegate;
        }
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
