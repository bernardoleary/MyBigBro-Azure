FROM nginx:1.27-alpine
COPY docker/web-ui/nginx.conf /etc/nginx/conf.d/default.conf
COPY Infostructure.MyBigBro.Web4/index.html /usr/share/nginx/html/
COPY Infostructure.MyBigBro.Web4/app /usr/share/nginx/html/app
COPY Infostructure.MyBigBro.Web4/scripts /usr/share/nginx/html/scripts
COPY Infostructure.MyBigBro.Web4/content /usr/share/nginx/html/content
COPY Infostructure.MyBigBro.Web4/angular-1.3.0-beta.3 /usr/share/nginx/html/angular-1.3.0-beta.3
EXPOSE 80
