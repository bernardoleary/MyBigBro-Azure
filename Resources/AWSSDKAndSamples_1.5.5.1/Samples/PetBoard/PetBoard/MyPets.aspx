<%@ Page Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MyPets.aspx.cs"
    Inherits="Petboard.MyPets" Title="Untitled Page" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <title>Petboard | My Pets</title>
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <div class="grid_16">
        <h2>
            <a class="add_new_pet_header" href="Default.aspx">All Pets</a> | My Pets | <a class="add_new_pet_header"
                href="PetDetails.aspx">Add a New Pet</a>
        </h2>
        <table cellpadding="0" cellspacing="0" border="0" class="mypets_table">
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
        </table>
    </div>
</asp:Content>
