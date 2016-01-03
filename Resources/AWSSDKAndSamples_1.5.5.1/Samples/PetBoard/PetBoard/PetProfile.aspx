<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="PetProfile.aspx.cs" Inherits="Petboard.PetProfile" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
    <title>Petboard | Pet Profile</title>
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <div class="grid_16">
        <h2>
            <a class="add_new_pet_header" href="Default.aspx">All Pets</a> |
            <asp:Literal runat="server" ID="PetNameHeader" />
        </h2>
        <div class="pet_profile_photo_wrapper">
            <img id="PetPhoto" runat="server" />
        </div>
        <div class="pet_profile_info">
            <div>
                <asp:Button CssClass="like_button" ID="LikeButton" runat="server" Text="I Like You!" OnClick="LikeButton_Click" />
            </div>
            <ul>
                <li>Liked by
                    <asp:Literal runat="server" ID="LikesCount" Text="0" />
                    other members </li>
                <li>Type:
                    <asp:Literal ID="TypeLiteral" runat="server" />
                </li>
                <li>Breed:
                    <asp:Literal ID="BreedLiteral" runat="server" />
                </li>
                <li>Sex:
                    <asp:Literal ID="SexLiteral" runat="server" />
                </li>
                <li>Likes:
                    <asp:Literal ID="LikesLiteral" runat="server" />
                </li>
                <li>Dislikes:
                    <asp:Literal ID="DislikesLiteral" runat="server" />
                </li>
            </ul>
        </div>
    </div>
</asp:Content>