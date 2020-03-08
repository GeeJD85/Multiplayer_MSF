using Aevien.UI;
using TMPro;

namespace GW.MasterServer
{
    public class PasswordResetCode_View : UIView
    {
        private TMP_InputField emailInputField;

        public string Email
        {
            get
            {
                return emailInputField != null ? emailInputField.text : string.Empty;
            }
        }

        protected override void Start()
        {
            base.Start();
            emailInputField = ChildComponent<TMP_InputField>("emailInputField");
        }
    }
}