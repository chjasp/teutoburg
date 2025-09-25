# Dockerfile
FROM python:3.11-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY main.py .

# Cloud Run sets $PORT; default to 8080 locally
ENV PORT=8080
CMD exec uvicorn main:app --host 0.0.0.0 --port ${PORT}
