FROM mcr.microsoft.com/dotnet/sdk:latest

ARG DEBIAN_FRONTEND=noninteractive
SHELL ["/bin/bash", "-c"]

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       ca-certificates curl git gnupg lsb-release apt-transport-https \
    && rm -rf /var/lib/apt/lists/*

# Install .NET SDK 8.0.417 (global.json target) alongside preinstalled SDKs
RUN curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && chmod +x /tmp/dotnet-install.sh \
    && /tmp/dotnet-install.sh --version 8.0.417 --install-dir /usr/share/dotnet --verbose \
    && rm /tmp/dotnet-install.sh

RUN set -eux; \
    arch="$(dpkg --print-architecture)"; \
    if [ "$arch" = "amd64" ]; then \
        # Learn / APT path (amd64)  [oai_citation:4‡Microsoft Learn](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local)
        apt-get update; \
        apt-get install -y gpg; \
        curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > /tmp/microsoft.gpg; \
        mv /tmp/microsoft.gpg /etc/apt/trusted.gpg.d/microsoft.gpg; \
        if [ -f /etc/os-release ]; then . /etc/os-release; fi; \
        if [ "${ID}" = "debian" ]; then \
          echo "deb [arch=amd64] https://packages.microsoft.com/debian/$(lsb_release -rs 2>/dev/null | cut -d'.' -f 1)/prod $(lsb_release -cs 2>/dev/null) main" > /etc/apt/sources.list.d/dotnetdev.list; \
        else \
          echo "deb [arch=amd64] https://packages.microsoft.com/repos/microsoft-ubuntu-$(lsb_release -cs 2>/dev/null)-prod $(lsb_release -cs 2>/dev/null) main" > /etc/apt/sources.list.d/dotnetdev.list; \
        fi; \
        apt-get update; \
        apt-get install -y azure-functions-core-tools-4; \
        rm -rf /var/lib/apt/lists/*; \
    else \
        # arm64: install Core Tools via alternative installer (per Learn note to use other installers/readme)  [oai_citation:5‡Microsoft Learn](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local)
        apt-get update; \
        apt-get install -y --no-install-recommends nodejs npm; \
        npm i -g azure-functions-core-tools@4 --unsafe-perm true; \
        rm -rf /var/lib/apt/lists/*; \
    fi

ENV AzureWebJobsFeatureFlags=EnableWorkerIndexing
WORKDIR /workspace
