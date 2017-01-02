<%@ Page Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Petboard.Default" Title="Untitled Page" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <title>Petboard</title>
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <div class="grid_16">
        <h2>
            All Pets | <a class="add_new_pet_header" href="MyPets.aspx">My Pets</a> | <a class="add_new_pet_header" href="PetDetails.aspx">Add a New Pet</a>
        </h2>
        <asp:Panel ID="SampleDataPromptPanel" runat="server" Visible="false">
            <h3>Create an Account/Add Sample Data</h3>
            <p runat="server" id="PreSignInMessage" visible="false">
                In order to use the Petboard application, you will need to create an account and/or sign-in to continue. Would you like to create an account or sign-in now?</p>
            <p runat="server" id="PostSignInMessage" visible="false">
                Would you like to populate this application using pets owned by the Amazon Web Services team? This action will add profile photos to Amazon S3 and information about each pet to Amazon SimpleDB.</p>
            <div class="form_line">
                <asp:Button CssClass="button_100" ID="YesButton" runat="server" Text="Yes" OnClick="YesSampleButton_Click" />
                <asp:Button CssClass="button_100" ID="NoButton" runat="server" Text="No" OnClick="NoSampleButton_Click" />
            </div>
        </asp:Panel>
        <asp:Repeater ID="PetRepeater" runat="server" OnItemDataBound="PetRepeater_ItemDataBound">
            <ItemTemplate>
                <div class="pet_photo">
                    <div runat="server" id="ImagePanel">
                        <asp:HyperLink ID="PetImageHyperLink" runat="server" ImageUrl='<%# Eval("PhotoThumbUrl") %>' />
                    </div>
                    <div>
                        <asp:HyperLink ID="PetTextHyperLink" runat="server" Text='<%# Eval("Name") %>' />
                    </div>
                </div>
            </ItemTemplate>
        </asp:Repeater>
    </div>
</asp:Content>
