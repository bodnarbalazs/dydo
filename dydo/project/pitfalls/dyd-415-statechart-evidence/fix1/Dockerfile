FROM node:22.23.0-bookworm
RUN apt-get update && apt-get install -y --no-install-recommends default-jdk-headless gcc libc6-dev procps && rm -rf /var/lib/apt/lists/*
ENV NODE_OPTIONS=--disable-warning=ExperimentalWarning
