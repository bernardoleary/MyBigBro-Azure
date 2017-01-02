BEGIN TRAN

select
*,
geometry::STPointFromText('POINT (' + cast(xcoord as varchar(30)) + ' ' + cast(ycoord as varchar(30)) + ')', 0).STBuffer(0.0001)
from GeoMarker
where MarkerDateTime > '2013-06-16 05:00:00.000'

ROLLBACK