FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1

WORKDIR /app

RUN addgroup --system collector \
    && adduser --system --ingroup collector collector

COPY pyproject.toml ./
COPY collector ./collector

RUN pip install --upgrade pip \
    && pip install .

USER collector

CMD ["python", "-m", "collector.main", "--skip-lookback"]
