using Aevien.UI;
using TMPro;

namespace GW.MasterServer
{
    public class EmailConfirmation_View : UIView
    {
        private TMP_InputField confirmationCodeInputField;

        public string ConfirmationCode
        {
            get
            {
                return confirmationCodeInputField != null ? confirmationCodeInputField.text : string.Empty;
            }
        }

        protected override void Start()
        {
            base.Start();
            confirmationCodeInputField = ChildComponent<TMP_InputField>("confirmationCodeInputField");
        }
    }
}