<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="AmazonDynamoDBSessionProviderSample._Default" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml" >
<head id="Head1" runat="server">
    <title>My AWS Enabled Application - AmazonDynamoDBSessionProviderSample</title>
    <link rel="stylesheet" href="styles/styles.css" type="text/css" media="screen" charset="utf-8"/>
</head>
<html>
<body>
<div id="content" class="container">
    <div class="section grid grid5">
        <h2>Session Details:</h2>
        <ul>
            <asp:Label ID="dynamoDBPlaceholder" runat="server"></asp:Label>
        </ul>
        <br />
        Page Count: <%= this.PageCount %>
        <br />
        <A HREF="javascript:history.go(0)">Click to refresh the page</A>
    </div>
</div>
</body>
</html>
