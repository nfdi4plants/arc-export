# arc-export
Automatic building of a Docker container for exporting ARCs to Arc.json

## local use

docker build . -t inv
docker run -v C:\Users\HLWei\OneDrive\NFDI\ISAConverterDSL\Invenio:/arc -it 13810bfaf675 dotnet fsi /invenioConverter.fsx -p /arc/InvenioArc