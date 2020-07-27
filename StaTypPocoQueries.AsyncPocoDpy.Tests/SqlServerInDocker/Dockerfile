FROM microsoft/mssql-server-linux

# make sure docker VM under Windows has at least 2GB RAM
# https://github.com/Microsoft/mssql-docker/issues/114

ARG SQLSERVERPASSWD
ENV SA_PASSWORD=$SQLSERVERPASSWD
ENV ACCEPT_EULA=Y

ENTRYPOINT /opt/mssql/bin/sqlservr
