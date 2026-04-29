FROM mcr.microsoft.com/playwright:v1.57.0-noble

WORKDIR /app

ENV NODE_ENV=production \
    HEADLESS=true \
    OUTPUT_PATH=/tmp/truthsocial-fetcher/latest-posts.json

COPY package*.json ./
RUN npm ci --omit=dev

COPY src ./src

USER pwuser

CMD ["npm", "run", "save"]
