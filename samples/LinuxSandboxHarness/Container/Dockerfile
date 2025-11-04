FROM mcr.microsoft.com/dotnet/runtime:8.0-jammy

ENV PRINTINGTOOLS_SANDBOX_PDF=/tmp/printingtools-output.pdf \
    GTK_USE_PORTAL=1 \
    GIO_USE_PORTALS=1

RUN apt-get update && \
    apt-get install -y --no-install-recommends libcups2 libgtk-3-0 ca-certificates && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /opt/printingtools
COPY bin/Release/net10.0/linux-x64/publish/ ./

ENTRYPOINT ["./PrintingTools.LinuxSandboxHarness", "--print"]
