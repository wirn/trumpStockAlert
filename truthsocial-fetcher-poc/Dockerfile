FROM mcr.microsoft.com/playwright:v1.59.1-noble

WORKDIR /app

ENV NODE_ENV=production \
    HEADLESS=true \
    OUTPUT_PATH=/tmp/truthsocial-fetcher/latest-posts.json \
    WRITE_DEBUG_SNAPSHOT=false \
    DEBUG_OUTPUT_DIR=/tmp/truthsocial-fetcher-output

COPY package*.json ./
RUN npm ci --omit=dev

COPY src ./src

USER pwuser

CMD ["npm", "run", "save"]
