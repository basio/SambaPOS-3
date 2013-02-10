using System.Collections.Generic;
using Samba.Domain.Models.Accounts;
using Samba.Services.Common;

namespace Samba.Services
{
    public interface ILocationService : IService
    {
        IEnumerable<AccountButton> GetCurrentLocations(AccountScreen locationScreen, int currentPageNo);
        IList<AccountButton> LoadLocations(string selectedLocationScreen);
        void SaveLocations();
    }
}
