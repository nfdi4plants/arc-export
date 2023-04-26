# arc-export
Automatic building of a Docker container for exporting ARCs to Arc.json

## local build
```shell
docker build . -t arc-export

docker run -v C:\Repos\ArcRepo:/arc -p arc
```

## download container first
```shell
docker pull ghcr.io/nfdi4plants/arc-export:main

docker run -v C:\Repos\ArcRepo:/arc -p arc
```
