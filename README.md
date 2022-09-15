# feedster
[![dockerpush](https://github.com/R4cc/feedster/actions/workflows/main.yml/badge.svg)](https://github.com/R4cc/feedster/actions/workflows/main.yml)

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
