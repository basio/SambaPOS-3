﻿using Samba.Infrastructure.Data;

namespace Samba.Domain.Models.Accounts
{
    public class AccountButton : Entity, IOrderable
    {
        public int Order { get; set; }
        public string Category { get; set; }
        public int XLocation { get; set; }
        public int YLocation { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
        public int CornerRadius { get; set; }
        public double Angle { get; set; }
        public int AccountId { get; set; }
        public virtual Account Account { get; set; }

        public string UserString
        {
            get { return Name + " [" + Category + "]"; }
        }

        public AccountButton()
        {
            Height = 70;
            Width = 70;
        }
    }
}
