FROM alpine:3.20

RUN apk add --no-cache ca-certificates curl

COPY run-scheduler.sh /usr/local/bin/run-scheduler
RUN sed -i 's/\r$//' /usr/local/bin/run-scheduler \
    && chmod +x /usr/local/bin/run-scheduler

CMD ["run-scheduler"]
