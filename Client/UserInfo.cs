using System.ComponentModel;

namespace Client
{
    public class UserInfo : INotifyPropertyChanged
    {
        private string _username;
        private bool _isOnline;

        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged(nameof(Username));
            }
        }

        public bool IsOnline
        {
            get => _isOnline;
            set
            {
                _isOnline = value;
                OnPropertyChanged(nameof(IsOnline));
            }
        }

        public UserInfo(string username, bool isOnline)
        {
            _username = username;
            _isOnline = isOnline;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

