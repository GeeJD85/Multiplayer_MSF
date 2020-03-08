using Aevien.UI;
using Barebones.MasterServer;

namespace GW.MasterServer
{
    public class LoadingInfo_View : PopupViewComponent
    {
        public override void OnOwnerStart()
        {
            Msf.Events.AddEventListener(Event_Keys.showLoadingInfo, OnShowLoadingInfoEventHandler);
            Msf.Events.AddEventListener(Event_Keys.hideLoadingInfo, OnHideLoadingInfoEventHandler);
        }

        private void OnShowLoadingInfoEventHandler(EventMessage message)
        {
            SetLables(message.GetData<string>());
            Owner.Show();
        }

        private void OnHideLoadingInfoEventHandler(EventMessage message)
        {
            Owner.Hide();
        }
    }
}