![feedster-logo-full-transparent](https://user-images.githubusercontent.com/48733309/190857523-6192d8b0-cd5a-42ba-8c00-de1cb1d008c7.png)

A semi-minimal RSS reader written in ASP.NET Blazor Server Side.

[![dockerpush](https://github.com/R4cc/feedster/actions/workflows/main.yml/badge.svg)](https://github.com/R4cc/feedster/actions/workflows/main.yml)

## Note
**This is more of a personal project that is in very early development than a production ready competitor to TinyRSS or similar projects and I am somewhat of a noob so expect minor problems :)**

## Features
The following features are built into the application
- Regular auto-fetching of RSS feeds with adjustable timeframe
- Custom feed names
- Dark Mode
- Custom folders for creating custom feeds
- Ready-to-use docker image.
- Mobile screen compatible.
- Webp image conversion for optimal performance

## To-Do
The following features are planned for the future
- Different post layout modes (card, grid, list, compact).
- ATOM support
- User authentication and user management system.
- Post title/description search

## Screenshots
### Home / Folder View
<table>
	<tbody>
		<tr>
			<td width="50%">
                <img width="500" alt="home-view" src="https://user-images.githubusercontent.com/48733309/192523278-1a8cb97a-ed8b-4768-b883-5d8914b88290.png">
			</td>
			<td width="50%">
                <img width="500" alt="2022-09-27 14_09_03-" src="https://user-images.githubusercontent.com/48733309/193017828-dbd7a11f-6b6e-4d11-9b80-36c9720b899b.png">
			</td>
		</tr>
	</tbody>
</table>

### Settings
<table>
	<tbody>
		<tr>
			<td width="50%">
                <img width="500" alt="2022-09-27 14_09_03-" src="https://user-images.githubusercontent.com/48733309/192523891-dcde046a-c946-4dfc-ae4a-a7060c93a478.png">
			</td>
            <td width="50%">
                <img width="500" alt="2022-09-27 14_09_03-" src="https://user-images.githubusercontent.com/48733309/193020202-47dd5095-6ec4-4502-b0c3-7602b4b5d265.png">
			</td>
		</tr>
	</tbody>
</table>

### Mobile View
<table>
	<tbody>
        <tr>
            <td width="auto">
                <img width="220px" src="https://user-images.githubusercontent.com/48733309/192523932-5e6ba4e3-46d8-4f5c-828a-12b31f0f059b.png">
            </td>
            <td width="auto">
                <img width="220px" src="https://user-images.githubusercontent.com/48733309/193018439-ad197743-26ff-4453-be46-c9b4b9bc96da.png">
            </td>
            <td width="auto">
                <img width="220px" src="https://user-images.githubusercontent.com/48733309/193018593-2a45969a-06a1-4426-98d6-3e2d1ede8365.png">
            </td>
            <td width="auto">
                <img width="220px"src="https://user-images.githubusercontent.com/48733309/193018127-17a050b5-0d99-4b62-b1cf-6fa0a0c86293.png">
            </td>
        </tr>
    </tbody>
</table>



## Docker

### For Nginx Reverse Proxy Users
Make sure that "Web Socket support" is enabled for this specific container. This is because Blazor Server communicates with a SignalR connection.

### Docker Run Command Example
In the below command, the application will be accessible at http://localhost:30080 on the host and the files including the database for all the articles would be stored in /your/path/data/ folder.
```
docker run -d \
    --restart unless-stopped \
    -p 30080:80 \
    -v  /your/path:/app/data \
    -v  /your/path:/app/images
    index.docker.io/nl2109/feedster:latest
```

### Docker Compose Example
In the below docker-compose.yml example, the application will be accessible at http://localhost:30080 on the host and the files including the database for all the articles would be stored in /your/path/data/ folder.
```
version: '3.4'
services:
    feedster:
        image: index.docker.io/nl2109/feedster:latest
        container_name: feedster
        restart: unless-stopped
        volumes:
          - /your/path:/app/data
	  - /your/path:/app/images
        ports:
          - '30080:80'
```
