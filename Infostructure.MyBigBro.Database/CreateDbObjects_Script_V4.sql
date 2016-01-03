﻿USE [MyBigBro]
GO
/****** Object:  Table [dbo].[WebCam]    Script Date: 06/03/2013 20:00:57 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[WebCam](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [varchar](50) NULL,
	[XCoord] [float] NULL,
	[YCoord] [float] NULL,
	[Url] [varchar](500) NULL,
	[RadiusOfVisibility] [float] NULL,
 CONSTRAINT [PK_WebCam] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING OFF
GO
/****** Object:  Table [dbo].[GeoMarker]    Script Date: 06/03/2013 20:00:57 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[GeoMarker](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[MarkerDateTime] [datetime] NOT NULL,
	[XCoord] [float] NOT NULL,
	[YCoord] [float] NOT NULL,
 CONSTRAINT [PK_GeoQuery] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CapturedImage]    Script Date: 01/01/2014 13:59:31 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[CapturedImage](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Url] [varchar](500) NULL,
	[WebCamId] [int] NULL,
	[DateTimeCaptured] [datetime] NULL,
	[IsThumbnailed] [bit] NULL,
	[Key] [varchar](100) NULL,
 CONSTRAINT [PK_CapturedImage] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING OFF
GO
ALTER TABLE [dbo].[CapturedImage] ADD  CONSTRAINT [DF_CapturedImage_IsThumbnailed]  DEFAULT ((0)) FOR [IsThumbnailed]
GO
/****** Object:  Table [dbo].[CapturedImageGeoMarker]    Script Date: 06/03/2013 20:00:57 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CapturedImageGeoMarker](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[CapturedImageId] [int] NOT NULL,
	[GeoMarkerId] [int] NOT NULL,
 CONSTRAINT [PK_CapturedImageGeoMarker] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_CapturedImageGeoMarker] ON [dbo].[CapturedImageGeoMarker] 
(
	[CapturedImageId] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
GO
/****** Object:  ForeignKey [FK_CapturedImage_WebCam]    Script Date: 06/03/2013 20:00:57 ******/
ALTER TABLE [dbo].[CapturedImage]  WITH CHECK ADD  CONSTRAINT [FK_CapturedImage_WebCam] FOREIGN KEY([WebCamId])
REFERENCES [dbo].[WebCam] ([Id])
GO
ALTER TABLE [dbo].[CapturedImage] CHECK CONSTRAINT [FK_CapturedImage_WebCam]
GO
/****** Object:  ForeignKey [FK_CapturedImageGeoMarker_CapturedImage]    Script Date: 06/03/2013 20:00:57 ******/
ALTER TABLE [dbo].[CapturedImageGeoMarker]  WITH CHECK ADD  CONSTRAINT [FK_CapturedImageGeoMarker_CapturedImage] FOREIGN KEY([CapturedImageId])
REFERENCES [dbo].[CapturedImage] ([Id])
GO
ALTER TABLE [dbo].[CapturedImageGeoMarker] CHECK CONSTRAINT [FK_CapturedImageGeoMarker_CapturedImage]
GO
/****** Object:  ForeignKey [FK_CapturedImageGeoMarker_GeoMarker]    Script Date: 06/03/2013 20:00:57 ******/
ALTER TABLE [dbo].[CapturedImageGeoMarker]  WITH CHECK ADD  CONSTRAINT [FK_CapturedImageGeoMarker_GeoMarker] FOREIGN KEY([GeoMarkerId])
REFERENCES [dbo].[GeoMarker] ([Id])
GO
ALTER TABLE [dbo].[CapturedImageGeoMarker] CHECK CONSTRAINT [FK_CapturedImageGeoMarker_GeoMarker]
GO
