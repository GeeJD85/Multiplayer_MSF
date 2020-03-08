using Aevien.UI;
using Barebones.MasterServer;

namespace GW.MasterServer
{
    public class OkDialog_View : PopupViewComponent
    {
        public override void OnOwnerStart()
        {
            Msf.Events.AddEventListener(Event_Keys.showOkDialogBox, OnShowOkDialogBoxEventHandler);
            Msf.Events.AddEventListener(Event_Keys.hideOkDialogBox, OnHideOkDialogBoxEventHandler);
        }

        private void OnShowOkDialogBoxEventHandler(EventMessage message)
        {
            var alertOkEventMessageData = message.GetData<OkDialogBox_ViewEventMessage>();

            SetLables(alertOkEventMessageData.Message);

            if (alertOkEventMessageData.OkCallback != null)
            {
                SetButtonsClick(() =>
                {
                    alertOkEventMessageData.OkCallback.Invoke();
                    Owner.Hide();
                });
            }
            else
            {
                SetButtonsClick(() =>
                {
                    Owner.Hide();
                });
            }

            Owner.Show();
        }

        private void OnHideOkDialogBoxEventHandler(EventMessage message)
        {
            Owner.Hide();
        }
    }
}