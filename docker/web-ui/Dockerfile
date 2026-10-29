FROM nginx:1.27-alpine
ENV API_UPSTREAM=api-service
COPY docker/web-ui/nginx.conf.template /etc/nginx/templates/default.conf.template
COPY docker/web-ui/nginx.conf /etc/nginx/conf.d/default.conf
COPY Infostructure.MyBigBro.Web4/index.html /usr/share/nginx/html/index.html
COPY Infostructure.MyBigBro.Web4/app /usr/share/nginx/html/app
COPY Infostructure.MyBigBro.Web4/scripts /usr/share/nginx/html/scripts
COPY Infostructure.MyBigBro.Web4/content /usr/share/nginx/html/content
EXPOSE 80
