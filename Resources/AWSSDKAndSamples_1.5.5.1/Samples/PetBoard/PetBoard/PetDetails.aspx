<%@ Page Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="PetDetails.aspx.cs"
    Inherits="Petboard.PetDetails" Title="Untitled Page" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <title>Petboard | Pet Details</title>
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <asp:HiddenField runat="server" ID="PhotoThumbUrl" />
    <div class="grid_16">
        <h2>
            <a class="add_new_pet_header" href="Default.aspx">All Pets</a> | <a class="add_new_pet_header"
                href="MyPets.aspx">My Pets</a> | Add a New Pet
            <asp:Literal runat="server" ID="PetNameHeader" />
        </h2>
        <asp:Panel runat="server" ID="FlashLiteralWrapper" Visible="false" CssClass="notify">
            <asp:Literal ID="FlashLiteral" runat="server" />
        </asp:Panel>
    </div>
    <div class="grid_7">
        <h3>
            <asp:Literal ID="StatsLiteral" runat="server" Text="Stats" />
        </h3>
        <table cellpadding="0" cellspacing="0" border="0">
            <tr>
                <td class="info_row_header">
                    Public
                </td>
                <td>
                    <asp:CheckBox ID="Public" runat="server" />
                </td>
            </tr>
            <tr>
                <td class="info_row_header">
                    Name
                </td>
                <td>
                    <asp:TextBox runat="server" ID="NameTextBox" />
                </td>
            </tr>
            <tr>
                <td class="info_row_header">
                    Type
                </td>
                <td>
                    <asp:DropDownList runat="server" ID="AnimalDropDownList">
                        <asp:ListItem Text="Dog" />
                        <asp:ListItem Text="Cat" />
                        <asp:ListItem Text="Hamster" />
                        <asp:ListItem Text="Fish" />
                        <asp:ListItem Text="Mouse" />
                        <asp:ListItem Text="Guinea Pig" />
                        <asp:ListItem Text="Bird" />
                        <asp:ListItem Text="Snake" />
                        <asp:ListItem Text="Snake" />
                        <asp:ListItem Text="Ferret" />
                    </asp:DropDownList>
                </td>
            </tr>
            <tr>
                <td class="info_row_header">
                    Breed
                </td>
                <td>
                    <asp:TextBox runat="server" ID="BreedTextBox" />
                </td>
            </tr>
            <tr>
                <td class="info_row_header">
                    Sex
                </td>
                <td>
                    <asp:RadioButtonList ID="SexDropDownList" runat="server" CssClass="radio_buttons">
                        <asp:ListItem Text="Male" />
                        <asp:ListItem Text="Female" />
                    </asp:RadioButtonList>
                </td>
            </tr>
            <tr>
                <td class="info_row_header">
                    Birthdate
                </td>
                <td>
                    <div>
                        <asp:Literal ID="AgeLiteral" runat="server" />
                    </div>
                    <div>
                        <asp:Label ID="YearDropDownListLabel" AssociatedControlID="YearDropDownList" runat="server"
                            Text="Year" />
                        <asp:DropDownList ID="YearDropDownList" runat="server" AutoPostBack="true" OnSelectedIndexChanged="YearDropDownList_SelectedIndexChanged" />
                        <asp:Label ID="MonthDropDownListLabel" AssociatedControlID="MonthDropDownList" runat="server"
                            Text="Month" />
                        <asp:DropDownList ID="MonthDropDownList" runat="server" AutoPostBack="true" DataValueField="Month"
                            DataTextField="MonthText" OnSelectedIndexChanged="MonthDropDownList_SelectedIndexChanged" />
                        <asp:Label ID="DayDropDownListLabel" AssociatedControlID="DayDropDownList" runat="server"
                            Text="Day" />
                        <asp:DropDownList ID="DayDropDownList" runat="server" AutoPostBack="true" OnSelectedIndexChanged="DayDropDownList_SelectedIndexChanged" />
                    </div>
                </td>
            </tr>
            <tr>
                <td class="info_row_header">
                    Likes
                </td>
                <td>
                    <asp:TextBox ID="LikesTextBox" TextMode="MultiLine" runat="server" />
                </td>
            </tr>
            <tr>
                <td class="info_row_header">
                    Dislikes
                </td>
                <td>
                    <asp:TextBox ID="DislikesTextBox" TextMode="MultiLine" runat="server" />
                </td>
            </tr>
        </table>
        <asp:Button ID="CancelEditButton" runat="server" Text="Cancel" OnClick="CancelEditsButton_Click"
            CssClass="button_100" />
        <asp:Button ID="SaveStatsButton" runat="server" Text="Save Stats" OnClick="SaveStatsButton_Click"
            CssClass="button_100" />
            <asp:Button ID="DeletePetButton" runat="server" Text="Delete Pet" OnClick="DeletePetButton_Click"
            CssClass="button_100" />
    </div>
    <div class="grid_9">
        <div id="PhotoPanel" runat="server" visible="false">
            <h3>
                Photos
            </h3>
            <h4>
                Add Photo
            </h4>
            <div class="pet_details_photo_wrapper">
                <div class="form_line">
                    <asp:Label ID="PhotoUploadLabel" runat="server" AssociatedControlID="PhotoUpload"
                        Text="Choose a Photo File to Upload" />
                </div>
                <div class="form_line">
                    <asp:FileUpload ID="PhotoUpload" runat="server" CssClass="file_upload" />
                    <asp:Button CssClass="button_100" ID="UploadButton" runat="server" OnClick="UploadButton_Click"
                        Text="Upload" />
                </div>
            </div>
            <h4>
                Existing Photos
            </h4>
            <asp:Repeater runat="server" ID="PhotoRepeater">
                <HeaderTemplate>
                    <div class="existing_photo_list">
                </HeaderTemplate>
                <ItemTemplate>
                    <img src='<%# Container.DataItem %>' />
                </ItemTemplate>
                <FooterTemplate>
                </FooterTemplate>
            </asp:Repeater>
        </div>
    </div>
</asp:Content>
