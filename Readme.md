# Wordpress Cache
A middleware for caching Wordpress to serve Wordpress as (almost) static website without PHP. 
Built using ASP.NET core combined with Redis database.

## Environment Variables
- `PUBLIC_ADDRESS` Website public URL
- `HOST` Hostname, typically domain name from `PUBLIC_ADDRESS`
- `CACHE_TTL` Expiry of cache in seconds

## Usage
Using docker compose

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
       - 80
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
       - 80
     environment:
       REDIS_HOST: redis:6379
       BACKEND_ADDRESS: http://wordpress
       PUBLIC_ADDRESS: https://www.example.com
       HOST: www.example.com
       CACHE_TTL: 7200

volumes:
    db_data:

```
