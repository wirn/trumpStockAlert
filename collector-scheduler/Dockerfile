FROM docker:cli

# curl: API health check; ca-certificates: HTTPS; both present in docker:cli base
# but listed explicitly so the intent is clear.
RUN apk add --no-cache ca-certificates curl

COPY run-scheduler.sh /usr/local/bin/run-scheduler
RUN sed -i 's/\r$//' /usr/local/bin/run-scheduler \
    && chmod +x /usr/local/bin/run-scheduler

CMD ["run-scheduler"]
