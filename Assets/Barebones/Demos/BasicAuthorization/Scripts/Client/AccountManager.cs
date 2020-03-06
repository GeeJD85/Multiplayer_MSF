using Aevien.UI;
using Barebones.Logging;
using Barebones.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Barebones.MasterServer.Examples
{
    public class AccountManager : BaseClientModule
    {
        private string outputMessage = string.Empty;

        private SignInView signinView;
        private SignUpView signupView;
        private PasswordResetView passwordResetView;
        private PasswordResetCodeView passwordResetCodeView;
        private EmailConfirmationView emailConfirmationView;

        [Header("Editor Debug Settings"), SerializeField]
        private string defaultUsername = "qwerty";
        [SerializeField]
        private string defaultEmail = "qwerty@mail.com";
        [SerializeField]
        private string defaultPassword = "qwerty123!@#";
        [SerializeField]
        private bool useDefaultCredentials = false;

        public UnityEvent OnSignedInEvent;
        public UnityEvent OnSignedOutEvent;
        public UnityEvent OnEmailConfirmedEvent;
        public UnityEvent OnPasswordChangedEvent;

        protected override void Initialize()
        {
            signinView = ViewsManager.GetView<SignInView>("SigninView");
            signupView = ViewsManager.GetView<SignUpView>("SignupView");
            passwordResetView = ViewsManager.GetView<PasswordResetView>("PasswordResetView");
            passwordResetCodeView = ViewsManager.GetView<PasswordResetCodeView>("PasswordResetCodeView");
            emailConfirmationView = ViewsManager.GetView<EmailConfirmationView>("EmailConfirmationView");

            MsfTimer.WaitForEndOfFrame(() =>
            {
                if (useDefaultCredentials && Application.isEditor)
                {
                    signinView.SetInputFieldsValues(defaultUsername, defaultPassword);
                    signupView.SetInputFieldsValues(defaultUsername, defaultEmail, defaultPassword);
                }

                if (IsConnected)
                {
                    Msf.Events.Invoke(EventKeys.hideLoadingInfo);
                    signinView.Show();
                }
                else
                {
                    Msf.Events.Invoke(EventKeys.showLoadingInfo, "Connecting to master server... Please wait!");
                }
            });
        }

        public void SignIn()
        {
            Msf.Events.Invoke(EventKeys.showLoadingInfo, "Signing in... Please wait!");

            logger.Debug("Signing in... Please wait!");

            MsfTimer.WaitForSeconds(1f, () =>
            {
                Msf.Client.Auth.SignIn(signinView.Username, signinView.Password, (accountInfo, error) =>
                {
                    Msf.Events.Invoke(EventKeys.hideLoadingInfo);

                    if (accountInfo != null)
                    {
                        signinView.Hide();

                        if (accountInfo.IsEmailConfirmed)
                        {
                            OnSignedInEvent?.Invoke();

                            Msf.Events.Invoke(EventKeys.showOkDialogBox,
                            new OkDialogBoxViewEventMessage($"You have successfuly signed in as {Msf.Client.Auth.AccountInfo.Username} and now you can create another part of your cool game!"));

                            logger.Debug($"You are successfully logged in as {Msf.Client.Auth.AccountInfo.Username}");
                        }
                        else
                        {
                            emailConfirmationView.Show();
                        }
                    }
                    else
                    {
                        outputMessage = $"An error occurred while signing in: {error}";
                        Msf.Events.Invoke(EventKeys.showOkDialogBox, new OkDialogBoxViewEventMessage(outputMessage, null));
                        logger.Error(outputMessage);
                    }
                });
            });
        }

        public void SignUp()
        {
            Msf.Events.Invoke(EventKeys.showLoadingInfo, "Signing up... Please wait!");

            MsfTimer.WaitForSeconds(1f, () =>
            {
                string username = signupView.Username;
                string email = signupView.Email;
                string password = signupView.Password;

                var credentials = new Dictionary<string, string>
                {
                    { "username", username },
                    { "email", email },
                    { "password", password }
                };

                Msf.Client.Auth.SignUp(credentials, (isSuccessful, error) =>
                {
                    Msf.Events.Invoke(EventKeys.hideLoadingInfo);

                    if (isSuccessful)
                    {
                        signupView.Hide();
                        signinView.SetInputFieldsValues(username, password);
                        signinView.Show();

                        logger.Debug($"You have successfuly signed up. Now you may sign in");
                    }
                    else
                    {
                        outputMessage = $"An error occurred while signing up: {error}";
                        Msf.Events.Invoke(EventKeys.showOkDialogBox, new OkDialogBoxViewEventMessage(outputMessage, null));
                        logger.Error(outputMessage);
                    }
                });
            });
        }

        public void SignInAsGuest()
        {
            Msf.Events.Invoke(EventKeys.showLoadingInfo, "Signing in... Please wait!");

            MsfTimer.WaitForSeconds(1f, () =>
            {
                Msf.Client.Auth.SignInAsGuest((accountInfo, error) =>
                {
                    Msf.Events.Invoke(EventKeys.hideLoadingInfo);

                    if (accountInfo != null)
                    {
                        signinView.Hide();

                        OnSignedInEvent?.Invoke();

                        Msf.Events.Invoke(EventKeys.showOkDialogBox,
                        new OkDialogBoxViewEventMessage($"You have successfuly signed in as {Msf.Client.Auth.AccountInfo.Username} and now you can create another part of your cool game!"));

                        logger.Debug($"You are successfully logged in as {Msf.Client.Auth.AccountInfo.Username}");
                    }
                    else
                    {
                        outputMessage = $"An error occurred while signing in: {error}";
                        Msf.Events.Invoke(EventKeys.showOkDialogBox, new OkDialogBoxViewEventMessage(outputMessage, null));
                        logger.Error(outputMessage);
                    }
                });
            });
        }

        public void SignOut()
        {
            OnSignedOutEvent?.Invoke();

            // Logout after diconnection
            Msf.Client.Auth.SignOut();

            ViewsManager.HideAllViews();

            Initialize();
        }

        public void Quit()
        {
            Msf.Runtime.Quit();
        }

        public void GetPasswordResetCode()
        {
            Msf.Events.Invoke(EventKeys.showLoadingInfo, "Sending reset password code... Please wait!");

            MsfTimer.WaitForSeconds(1f, () =>
            {
                Msf.Client.Auth.RequestPasswordReset(passwordResetCodeView.Email, (isSuccessful, error) =>
                {
                    Msf.Events.Invoke(EventKeys.hideLoadingInfo);

                    if (isSuccessful)
                    {
                        passwordResetCodeView.Hide();
                        passwordResetView.Show();

                        Msf.Events.Invoke(EventKeys.showOkDialogBox, new OkDialogBoxViewEventMessage($"We have sent an email with reset code to your address '{passwordResetCodeView.Email}'", null));
                    }
                    else
                    {
                        outputMessage = $"An error occurred while password reset code: {error}";
                        Msf.Events.Invoke(EventKeys.showOkDialogBox, new OkDialogBoxViewEventMessage(outputMessage, null));
                        logger.Error(outputMessage);
                    }
                });
            });
        }

        public void ResetPassword()
        {
            Msf.Events.Invoke(EventKeys.showLoadingInfo, "Changing password... Please wait!");

            MsfTimer.WaitForSeconds(1f, () =>
            {
                Msf.Client.Auth.ChangePassword(new PasswordChangeData()
                {
                    Email = passwordResetCodeView.Email,
                    Code = passwordResetView.ResetCode,
                    NewPassword = passwordResetView.NewPassword
                },
                (isSuccessful, error) =>
                {
                    Msf.Events.Invoke(EventKeys.hideLoadingInfo);

                    if (isSuccessful)
                    {
                        passwordResetView.Hide();
                        signinView.Show();

                        OnPasswordChangedEvent?.Invoke();

                        Msf.Events.Invoke(EventKeys.showOkDialogBox, new OkDialogBoxViewEventMessage("You have successfuly changed your password. Now you can sign in.", null));
                    }
                    else
                    {
                        outputMessage = $"An error occurred while changing password: {error}";
                        Msf.Events.Invoke(EventKeys.showOkDialogBox, new OkDialogBoxViewEventMessage(outputMessage, null));
                        logger.Error(outputMessage);
                    }
                });
            });
        }

        public void RequestConfirmationCode()
        {
            Msf.Events.Invoke(EventKeys.showLoadingInfo, "Sending confirmation code... Please wait!");

            MsfTimer.WaitForSeconds(1f, () =>
            {
                Msf.Client.Auth.RequestEmailConfirmationCode((isSuccessful, error) =>
                {
                    Msf.Events.Invoke(EventKeys.hideLoadingInfo);

                    if (isSuccessful)
                    {
                        emailConfirmationView.Show();
                        Msf.Events.Invoke(EventKeys.showOkDialogBox, new OkDialogBoxViewEventMessage($"We have sent an email with confirmation code to your address '{Msf.Client.Auth.AccountInfo.Email}'", null));
                    }
                    else
                    {
                        outputMessage = $"An error occurred while requesting confirmation code: {error}";
                        Msf.Events.Invoke(EventKeys.showOkDialogBox, new OkDialogBoxViewEventMessage(outputMessage, null));
                        logger.Error(outputMessage);
                    }
                });
            });
        }

        public void ConfirmAccount()
        {
            Msf.Events.Invoke(EventKeys.showLoadingInfo, "Confirming your account... Please wait!");

            MsfTimer.WaitForSeconds(1f, () =>
            {
                string confirmationCode = emailConfirmationView.ConfirmationCode;

                Msf.Client.Auth.ConfirmEmail(confirmationCode, (isSuccessful, error) =>
                {
                    Msf.Events.Invoke(EventKeys.hideLoadingInfo);

                    if (isSuccessful)
                    {
                        emailConfirmationView.Hide();
                        OnEmailConfirmedEvent?.Invoke();
                    }
                    else
                    {
                        outputMessage = $"An error occurred while confirming yor account: {error}";
                        Msf.Events.Invoke(EventKeys.showOkDialogBox, new OkDialogBoxViewEventMessage(outputMessage, null));
                        logger.Error(outputMessage);
                    }
                });
            });
        }

        protected override void OnConnectedToMaster()
        {
            Msf.Events.Invoke(EventKeys.hideLoadingInfo);

            if (Msf.Client.Auth.IsSignedIn)
            {
                OnSignedInEvent?.Invoke();
            }
            else
            {
                signinView.Show();
            }
        }

        protected override void OnDisconnectedFromMaster()
        {
            // Logout after diconnection
            Msf.Client.Auth.SignOut();

            Msf.Events.Invoke(EventKeys.showOkDialogBox,
                new OkDialogBoxViewEventMessage("The connection to the server has been lost. "
                + "Please try again or contact the developers of the game or your internet provider.",
                () =>
                {
                    ViewsManager.HideAllViews();
                    Initialize();
                    ConnectionToMaster.Instance.StartConnection();
                }));
        }
    }
}