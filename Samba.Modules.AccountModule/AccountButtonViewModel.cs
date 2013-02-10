using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Samba.Domain.Models.Accounts;
using Samba.Localization;
using Samba.Presentation.Common;
using Samba.Presentation.Common.Services;

namespace Samba.Modules.AccountModule
{
    public class AccountButtonViewModel : ObservableObject, IDiagram
    {
        private readonly ICommand _actionCommand;

        public AccountButtonViewModel(AccountButton model, AccountScreen screen)
            : this(model, screen, null)
        {

        }

        public AccountButtonViewModel(AccountButton model, AccountScreen screen, ICommand actionCommand)
        {
            _isEnabled = true;
            _actionCommand = actionCommand;
            _screen = screen;
            Model = model;
        }

        private readonly AccountScreen _screen;

        [Browsable(false)]
        public AccountButton Model { get; set; }

        [LocalizedDisplayName(ResourceStrings.Location)]
        public string Name { get { return Model.Name; } }

        private string _buttonColor;
        [Browsable(false)]
        public string ButtonColor
        {
            get { return _buttonColor; }
            set
            {
                if (_buttonColor != value)
                {
                    _buttonColor = value;
                    RaisePropertyChanged(() => ButtonColor);
                }
            }
        }

        [Browsable(false)]
        public double ButtonHeight { get { return _screen.ButtonHeight > 0 ? _screen.ButtonHeight : double.NaN; } }

        [Browsable(false)]
        public ICommand Command
        {
            get { return _actionCommand; }
        }

        private bool _isEnabled;

        [Browsable(false)]
        public bool IsEnabled
        {
            get { return _isEnabled; }
            set { _isEnabled = value; RaisePropertyChanged(() => IsEnabled); }
        }

        [Browsable(false)]
        public string Caption
        {
            get { return Model.Name; }
            set { Model.Name = value; RaisePropertyChanged(() => Caption); }
        }

        public int X
        {
            get { return Model.XLocation; }
            set { Model.XLocation = value; RaisePropertyChanged(() => X); }
        }

        public int Y
        {
            get { return Model.YLocation; }
            set { Model.YLocation = value; RaisePropertyChanged(() => Y); }
        }

        [LocalizedDisplayName(ResourceStrings.Height)]
        public int Height
        {
            get { return Model.Height; }
            set { Model.Height = value; RaisePropertyChanged(() => Height); }
        }

        [LocalizedDisplayName(ResourceStrings.Width)]
        public int Width
        {
            get { return Model.Width; }
            set { Model.Width = value; RaisePropertyChanged(() => Width); }
        }

        [Browsable(false)]
        public Transform RenderTransform
        {
            get { return new RotateTransform(Model.Angle); }
            set { Model.Angle = ((RotateTransform)value).Angle; }
        }

        [LocalizedDisplayName(ResourceStrings.Angle)]
        public double Angle
        {
            get { return Model.Angle; }
            set
            {
                Model.Angle = value;
                RaisePropertyChanged(() => Angle);
                RaisePropertyChanged(() => RenderTransform);
            }
        }

        [LocalizedDisplayName(ResourceStrings.CornerRadius)]
        public CornerRadius CornerRadius
        {
            get { return new CornerRadius(Model.CornerRadius); }
            set { Model.CornerRadius = Convert.ToInt32(value.BottomLeft); RaisePropertyChanged(() => CornerRadius); }
        }

        public void EditProperties()
        {
            InteractionService.UserIntraction.EditProperties(this);
        }
    }
}
