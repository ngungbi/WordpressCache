# Wordpress Cache
A middleware for caching Wordpress to serve Wordpress as (almost) static website without PHP. 
Built using ASP.NET core combined with Redis database.

## Dependencies
- Redis database

## Environment Variables
- `PUBLIC_ADDRESS` Website public URL
- `HOST` Hostname, typically domain name from `PUBLIC_ADDRESS`
- `CACHE_TTL` Expiry of cache in seconds

## Installation
1. Using docker compose
```
version: '1'

services:
   db:
     image: mysql:5.7
     volumes:
       - db_data:/var/lib/mysql
     restart: always
     environment:
       MYSQL_ROOT_PASSWORD: ${MYSQL_DATABASE_PASSWORD}
       MYSQL_DATABASE: wordpress
       MYSQL_USER: wordpress
       MYSQL_PASSWORD: wordpress

   wordpress:
     image: wordpress:latest
     ports:
       - 30002:80
     restart: always
     environment:
       WORDPRESS_DB_HOST: db:3306
       WORDPRESS_DB_USER: wordpress
       WORDPRESS_DB_PASSWORD: wordpress
       
   redis:
     image: redis:latest
     restart: always
     
   cache:
     image: ngungbi/wordpress-cache
     ports:
       - 30003:80
     environment:
       REDIS_HOST: redis:6379
       BACKEND_ADDRESS: http://wordpress
       PUBLIC_ADDRESS: https://www.example.com
       HOST: www.example.com
       CACHE_TTL: 7200

volumes:
    db_data:

```

2. Setup Nginx to cache only GET requests
```
server {
    server_name example.com www.example.com;

    client_max_body_size 50M;

    # bypass admin page
    location /wp-admin {
        proxy_pass http://localhost:30002;
        proxy_set_header Host $http_host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # use cache only for GET
    location / {
        proxy_pass http://localhost:30003;
        proxy_set_header Host $http_host;

        # for method other than GET, bypass
        limit_except GET {
            proxy_pass http://localhost:30002;
        }

        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```
