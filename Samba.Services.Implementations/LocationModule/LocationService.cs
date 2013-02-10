using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Samba.Domain.Models.Accounts;
using Samba.Domain.Models.Tickets;
using Samba.Infrastructure.Data;
using Samba.Localization.Properties;
using Samba.Persistance.Data;
using Samba.Services.Common;

namespace Samba.Services.Implementations.LocationModule
{
    [Export(typeof(ILocationService))]
    public class LocationService : AbstractService, ILocationService
    {
        private IWorkspace _locationWorkspace;
        private readonly IApplicationState _applicationState;

        [ImportingConstructor]
        public LocationService(IApplicationState applicationState, IApplicationStateSetter applicationStateSetter)
        {
            _applicationState = applicationState;

            ValidatorRegistry.RegisterSaveValidator(new NonDuplicateSaveValidator<AccountButton>(string.Format(Resources.SaveErrorDuplicateItemName_f, Resources.Location)));
            ValidatorRegistry.RegisterDeleteValidator(new LocationDeleteValidator());
            ValidatorRegistry.RegisterDeleteValidator(new LocationScreenDeleteValidator());
        }

        public IEnumerable<AccountButton> GetCurrentLocations(AccountScreen locationScreen, int currentPageNo)
        {
            var selectedLocationScreen = _applicationState.SelectedLocationScreen;

            if (selectedLocationScreen != null)
            {
                if (selectedLocationScreen.PageCount > 1)
                {
                    return selectedLocationScreen.Buttons
                         .OrderBy(x => x.Order)
                         .Skip(selectedLocationScreen.ItemCountPerPage * currentPageNo)
                         .Take(selectedLocationScreen.ItemCountPerPage);
                }
                return selectedLocationScreen.Buttons;
            }
            return new List<AccountButton>();
        }


        public IList<AccountButton> LoadLocations(string selectedLocationScreen)
        {
            if (_locationWorkspace != null)
            {
                _locationWorkspace.CommitChanges();
            }
            _locationWorkspace = WorkspaceFactory.Create();
            return _locationWorkspace.Single<AccountScreen>(x => x.Name == selectedLocationScreen).Buttons;
        }

        public void SaveLocations()
        {
            if (_locationWorkspace != null)
            {
                _locationWorkspace.CommitChanges();
                _locationWorkspace = null;
            }
        }

        public override void Reset()
        {

        }
    }

    internal class LocationDeleteValidator : SpecificationValidator<AccountButton>
    {
        public override string GetErrorMessage(AccountButton model)
        {
            if (Dao.Exists<AccountScreen>(x => x.Buttons.Any(y => y.Id == model.Id)))
                return string.Format(Resources.DeleteErrorUsedBy_f, Resources.Location, Resources.LocationScreen);
            return "";
        }
    }

    internal class LocationScreenDeleteValidator : SpecificationValidator<AccountScreen>
    {
        public override string GetErrorMessage(AccountScreen model)
        {
            if (Dao.Exists<Department>(x => x.LocationScreens.Any(y => y.Id == model.Id)))
                return string.Format(Resources.DeleteErrorUsedBy_f, Resources.LocationScreen, Resources.Department);
            return "";
        }
    }
}
