USE [MyBigBro]
GO
/****** Object:  Table [dbo].[WebCam]    Script Date: 06/03/2013 18:41:02 ******/
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
/****** Object:  Table [dbo].[GeoMarker]    Script Date: 06/03/2013 18:41:02 ******/
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
/****** Object:  Table [dbo].[CapturedImage]    Script Date: 06/03/2013 18:41:02 ******/
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
 CONSTRAINT [PK_CapturedImage] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING OFF
GO
/****** Object:  Table [dbo].[CapturedImageGeoMarker]    Script Date: 06/03/2013 18:41:02 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CapturedImageGeoMarker](
	[CapturedImageId] [int] NOT NULL,
	[GeoMarkerId] [int] NOT NULL
) ON [PRIMARY]
GO
/****** Object:  ForeignKey [FK_CapturedImage_WebCam]    Script Date: 06/03/2013 18:41:02 ******/
ALTER TABLE [dbo].[CapturedImage]  WITH CHECK ADD  CONSTRAINT [FK_CapturedImage_WebCam] FOREIGN KEY([WebCamId])
REFERENCES [dbo].[WebCam] ([Id])
GO
ALTER TABLE [dbo].[CapturedImage] CHECK CONSTRAINT [FK_CapturedImage_WebCam]
GO
/****** Object:  ForeignKey [FK_CapturedImageGeoMarker_CapturedImage]    Script Date: 06/03/2013 18:41:02 ******/
ALTER TABLE [dbo].[CapturedImageGeoMarker]  WITH CHECK ADD  CONSTRAINT [FK_CapturedImageGeoMarker_CapturedImage] FOREIGN KEY([CapturedImageId])
REFERENCES [dbo].[CapturedImage] ([Id])
GO
ALTER TABLE [dbo].[CapturedImageGeoMarker] CHECK CONSTRAINT [FK_CapturedImageGeoMarker_CapturedImage]
GO
/****** Object:  ForeignKey [FK_CapturedImageGeoMarker_GeoMarker]    Script Date: 06/03/2013 18:41:02 ******/
ALTER TABLE [dbo].[CapturedImageGeoMarker]  WITH CHECK ADD  CONSTRAINT [FK_CapturedImageGeoMarker_GeoMarker] FOREIGN KEY([GeoMarkerId])
REFERENCES [dbo].[GeoMarker] ([Id])
GO
ALTER TABLE [dbo].[CapturedImageGeoMarker] CHECK CONSTRAINT [FK_CapturedImageGeoMarker_GeoMarker]
GO
