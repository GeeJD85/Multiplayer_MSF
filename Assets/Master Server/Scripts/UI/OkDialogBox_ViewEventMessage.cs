using UnityEngine.Events;

namespace GW.MasterServer
{
    public class OkDialogBox_ViewEventMessage
    {
        public OkDialogBox_ViewEventMessage() { }

        public OkDialogBox_ViewEventMessage(string message)
        {
            Message = message;
            OkCallback = null;
        }

        public OkDialogBox_ViewEventMessage(string message, UnityAction okCallback)
        {
            Message = message;
            OkCallback = okCallback;
        }

        public string Message { get; set; }
        public UnityAction OkCallback { get; set; }
    }
}