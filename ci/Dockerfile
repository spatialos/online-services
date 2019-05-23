FROM microsoft/dotnet

# Download and install gosu, so we can drop privs after the container starts.
RUN curl -LSs -o /usr/local/bin/gosu -SL "https://github.com/tianon/gosu/releases/download/1.4/gosu-$(dpkg --print-architecture)" \
    && chmod +x /usr/local/bin/gosu

# Download dotnet format and make it accessible by all users
RUN dotnet tool install --tool-path /usr/local/bin dotnet-format \
    && chmod +x /usr/local/bin/dotnet-format

COPY scripts/ /usr/local/bin
ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
