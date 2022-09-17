![feedster-logo-full-transparent](https://user-images.githubusercontent.com/48733309/190857523-6192d8b0-cd5a-42ba-8c00-de1cb1d008c7.png)

A lightweight minimal RSS reader written in ASP.NET Blazor Server Side.

[![dockerpush](https://github.com/R4cc/feedster/actions/workflows/main.yml/badge.svg)](https://github.com/R4cc/feedster/actions/workflows/main.yml)

## Features
The following features are built into the application:
- Regular auto-fetching of RSS feeds with adjustable timeframe
- Custom feed names
- Article Tags
- Custom folders for creating custom feeds
- Ready-to-use docker image.
- Mobile screen compatible.

## To-Do
The following features are planned for the future
- Different post layout modes (card, grid, list, compact).
- ATOM support
- User authentication and user management system.
- Customization options (themes)
- Error handling
- Post title/description search
- Improved image caching and delivering (optimizations)

## Screenshots
<table>
	<tbody>
		<tr>
			<td width="25%">
				Home View
                <img src="https://user-images.githubusercontent.com/48733309/190857294-13027837-b2d9-4894-9857-135427313eee.png">
			</td>
			<td width="25%">
				Folder View(Custom Feed)
                <img src="https://user-images.githubusercontent.com/48733309/190857333-c5dd5bb1-1eda-4c02-9d2c-0e39c05075fb.png">
			</td>
			<td width="25%">
				Folder Manage Page
                <img src="https://user-images.githubusercontent.com/48733309/190857273-6f571254-96b2-4e81-b59c-254aa70c6948.png">
			</td>
			<td width="25%">
				Feed Management
                <img src="https://user-images.githubusercontent.com/48733309/190857360-335c344f-6923-4c65-a987-17823fa06dae.png">
			</td>
		</tr>
	</tbody>
</table>

## Docker
### Docker Run Command Example
In the below command, the application will be accessible at http://localhost:30080 on the host and the files including the database for all the articles would be stored in /your/path/data/ folder.
```
docker run -d \
    --restart unless-stopped \
    -p 30080:80 \
    -v  /your/path:/app/data
    ghcr.io/nl2109/feedster:latest 
```

### Docker Compose Example
In the below docker-compose.yml example, the application will be accessible at http://localhost:30080 on the host and the files including the database for all the articles would be stored in /your/path/data/ folder.
```
version: '3.4'
services:
    feedster:
        image: ghcr.io/nl2109/feedster:latest
        container_name: feedster
        restart: unless-stopped
        volumes:
          - /your/path:/app/data
        ports:
          - '30080:80'
```
