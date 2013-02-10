using System.Collections.Generic;
using Samba.Domain.Models.Accounts;
using Samba.Domain.Models.Tickets;
using Samba.Domain.Models.Users;

namespace Samba.Services
{
    public interface IApplicationStateSetter
    {
        void SetCurrentTicket(Ticket ticket);
        void SetCurrentLoggedInUser(User user);
        void SetCurrentDepartment(int departmentId);
        void SetCurrentApplicationScreen(AppScreens appScreen);
        void SetSelectedLocationScreen(AccountScreen locationScreen);
        void SetSelectedAccountForTicket(Account account);
        void ResetWorkPeriods();
    }
}