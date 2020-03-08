using Aevien.UI;
using TMPro;

namespace GW.MasterServer
{
    public class SignIn_View : UIView
    {
        private TMP_InputField usernameInputField;
        private TMP_InputField passwordInputField;

        public string Username
        {
            get
            {
                return usernameInputField != null ? usernameInputField.text : string.Empty;
            }
        }

        public string Password
        {
            get
            {
                return passwordInputField != null ? passwordInputField.text : string.Empty;
            }
        }

        protected override void Start()
        {
            base.Start();

            usernameInputField = ChildComponent<TMP_InputField>("usernameInputField");
            passwordInputField = ChildComponent<TMP_InputField>("passwordInputField");
        }

        public void SetInputFieldsValues(string username, string password)
        {
            usernameInputField.text = username;
            passwordInputField.text = password;
        }
    }
}