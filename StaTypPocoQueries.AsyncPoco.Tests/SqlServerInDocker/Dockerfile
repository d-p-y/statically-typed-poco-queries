FROM microsoft/mssql-server-linux

# official image is broken as of 2016-11-23, fix based on https://hub.docker.com/r/moisei/mssql-server-linux/
# thanks moisei!

RUN apt update && apt install -y python sudo
ARG SQLSERVERPASSWD
ENV SA_PASSWORD=$SQLSERVERPASSWD
RUN /opt/mssql/bin/sqlservr-setup --accept-eula --set-sa-password

ENTRYPOINT /opt/mssql/bin/sqlservr
