using Aevien.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Barebones.MasterServer.Examples.BasicAuthorization
{
    public class OkDialogView : PopupViewComponent
    {
        public override void OnOwnerStart()
        {
            Msf.Events.AddEventListener(EventKeys.showOkDialogBox, OnShowOkDialogBoxEventHandler);
            Msf.Events.AddEventListener(EventKeys.hideOkDialogBox, OnHideOkDialogBoxEventHandler);
        }

        private void OnShowOkDialogBoxEventHandler(EventMessage message)
        {
            var alertOkEventMessageData = message.GetData<OkDialogBoxViewEventMessage>();

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