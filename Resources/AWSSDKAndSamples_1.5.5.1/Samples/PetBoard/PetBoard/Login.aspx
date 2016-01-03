<%@ Page Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Login.aspx.cs"
    Inherits="Petboard.Login" Title="Untitled Page" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <title>Petboard | Sign In / Create Account</title>
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">

    <script type="text/javascript">
        $(document).ready(function() {
            // make sure we're clicking the right button
            // depending on which set of fields we're typing in
            $(".signin_textbox").keypress(function(e) {
                if (e.which == 13) {
                    $('form').submit(function() { return false; });
                    $("#<%= LoginButton.ClientID %>").click();
                }

            });
            $(".signup_textbox").keypress(function(e) {
                if (e.which == 13) {
                    $('form').submit(function() { return false; });
                    $("#<%= SignupButton.ClientID %>").click();
                }
            });
        });
    </script>

    <div class="grid_16 login_page_header">
        <div class="prefix_2 grid_6">
            Sign In to Your Account
        </div>
        <div class="grid_6">
            Create a New Account
        </div>
    </div>
    <div class="prefix_2 grid_14 login_page_notify">
        <pc:Notify ID="Notify" runat="server" CssClass="notify" />
    </div>
    <div class="prefix_2 grid_6">
        <div class="form_line">
            <div class="">
                <asp:Label CssClass="" ID="UserNameTextBoxLabel" AssociatedControlID="UserNameTextBox"
                    runat="server" Text="User Name" />
            </div>
            <div class="">
                <asp:TextBox CssClass="signin_textbox" ID="UserNameTextBox" runat="server" />
            </div>
        </div>
        <div class="form_line">
            <div class="">
                <asp:Label CssClass="" ID="PasswordTextBoxLabel" AssociatedControlID="PasswordTextBox"
                    runat="server" Text="Password" />
            </div>
            <div class="">
                <asp:TextBox CssClass="signin_textbox" ID="PasswordTextBox" TextMode="Password" runat="server" />
            </div>
        </div>
        <div class="error_rollup">
            <ul runat="server" id="SigninErrors" visible="false">
                <li class="error_rollup_header">Oops! Please Fix These Errors
                    <ul class="error_rollup_messages">
                        <li runat="server" id="UserNameIsMissing" visible="false">A user-name is required.</li>
                        <li runat="server" id="PasswordIsMissing" visible="false">A password is required.</li>
                        <li runat="server" id="UnrecognizedLogin" visible="false">The user-name and password combination was not recognized.</li>
                    </ul>
                </li>
            </ul>
            <asp:Button CssClass="button_100" ID="LoginButton" runat="server" Text="Sign In"
                OnClick="LoginButton_Click" />
        </div>
    </div>
    <div>
        <div class="grid_6 suffix_2">
            <p class="login_page_security_note">
                Please note: Petboard does not support password
                encryption; password information is stored unencrypted.
            </p>
            <div class="form_line">
                <div class="">
                    <asp:Label CssClass="" ID="NewUserNameTextBoxLabel" AssociatedControlID="NewUserNameTextBox"
                        runat="server" Text="User Name" />
                </div>
                <div class="">
                    <asp:TextBox CssClass="signup_textbox" ID="NewUserNameTextBox" runat="server" />
                </div>
            </div>
            <div class="form_line">
                <div class="">
                    <asp:Label CssClass="" ID="EmailTextBoxLabel" AssociatedControlID="EmailTextBox"
                        runat="server" Text="E-mail Address" />
                </div>
                <div class="">
                    <asp:TextBox CssClass="signup_textbox" ID="EmailTextBox" runat="server" />
                </div>
            </div>
            <div class="form_line">
                <div class="">
                    <asp:Label CssClass="" ID="NewPasswordTextBoxLabel" AssociatedControlID="NewPasswordTextBox"
                        runat="server" Text="Password" />
                </div>
                <div class="">
                    <asp:TextBox CssClass="signup_textbox" ID="NewPasswordTextBox" TextMode="Password"
                        runat="server" />
                </div>
            </div>
            <div class="form_line">
                <div class="">
                    <asp:Label CssClass="" ID="ConfirmNewPasswordTextBoxLabel" AssociatedControlID="ConfirmNewPasswordTextBox"
                        runat="server" Text="Confirm Password" />
                </div>
                <div class="">
                    <asp:TextBox CssClass="signup_textbox" ID="ConfirmNewPasswordTextBox" TextMode="Password"
                        runat="server" />
                </div>
            </div>
            <div class="error_rollup">
                <ul runat="server" id="SignupErrors" visible="false">
                    <li class="error_rollup_header">Oops! Please Fix These Errors
                        <ul class="error_rollup_messages">
                            <li runat="server" id="NewUserNameIsMissing" visible="false">A user name is required.</li>
                            <li runat="server" id="NewPasswordIsMissing" visible="false">A password is required.</li>
                            <li runat="server" id="UserNameTaken" visible="false">The user name is already registered.</li>
                            <li runat="server" id="EmailAlreadyRegistered" visible="false">The e-mail address is
                                already registered.</li>
                            <li runat="server" id="InvalidUserName" visible="false">Invalid characters in the user-name. Only alphanumeric characters
                                allowed in a user-name.</li>
                            <li runat="server" id="PasswordsDontMatch" visible="false">The passwords do not match.</li>
                        </ul>
                    </li>
                </ul>
                <asp:Button CssClass="button_100" ID="SignupButton" runat="server" Text="Sign Up"
                    OnClick="SignupButton_Click" />
            </div>
        </div>
    </div>
</asp:Content>