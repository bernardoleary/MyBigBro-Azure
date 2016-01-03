<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Error.aspx.cs" Inherits="Petboard.Error" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <div class="grid_16">
        <h2>
            Oops! An error has occured
        </h2>
        <% if (Context.Error != null)
           {
        %>
        <div>
            <%= Context.Error.Message%>
        </div>
        <div>
            <%= Context.Error.StackTrace%>
        </div>
        <%
            }
        %>
    </div>
</asp:Content>
