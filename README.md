# feedster
A lightweight minimal RSS reader written in ASP.NET Blazor

# Deployment

## Docker Compose
```
version: '3.4'
services:
feedster:
    image: index.docker.io/nl2109/feedster:latest
    container_name: feedster
    restart: unless-stopped
    volumes:
      - /your/path:/app/data
    ports:
      - '30080:80'
```
