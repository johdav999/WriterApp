using System;

namespace WriterApp.Application.State
{
    public sealed class AppHeaderState
    {
        private string? _documentTitle;

        public event Action? OnChange;

        public string? DocumentTitle
        {
            get => _documentTitle;
            set
            {
                if (string.Equals(_documentTitle, value, StringComparison.Ordinal))
                {
                    return;
                }

                _documentTitle = value;
                OnChange?.Invoke();
            }
        }
    }
}
