import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const API_KEY = __ENV.API_KEY || '';
const CLIENT_ID = __ENV.CLIENT_ID || 'client_acme_corp';

export const options = {
  vus: Number(__ENV.VUS || 10),
  duration: __ENV.DURATION || '1m',
  thresholds: {
    http_req_failed: ['rate<0.01'],
    'http_req_duration{endpoint:post}': ['p(95)<500'],
    'http_req_duration{endpoint:get}': ['p(95)<400']
  },
};

function headers() {
  const h = { 'Content-Type': 'application/json' };
  if (API_KEY) h['Authorization'] = `Bearer ${API_KEY}`;
  if (CLIENT_ID) h['X-Client-ID'] = CLIENT_ID;
  return h;
}

function randomId(prefix) {
  return `${prefix}_${Math.random().toString(36).slice(2, 10)}_${Date.now()}`;
}

function postSurveyResponse() {
  const responseId = randomId('resp');
  const payload = JSON.stringify({
    surveyId: 'survey_2024_customer_satisfaction',
    clientId: CLIENT_ID,
    responseId,
    responses: {
      npsScore: Math.floor(Math.random() * 11),
      satisfaction: 'satisfied',
      customFields: { feature_rating: Math.floor(Math.random() * 10) + 1 }
    },
    metadata: {
      timestamp: new Date().toISOString(),
      userAgent: 'k6-load-test',
      ipAddress: '127.0.0.1',
      deviceType: 'desktop'
    }
  });

  const res = http.post(`${BASE_URL}/api/v1/survey-responses`, payload, { headers: headers(), tags: { endpoint: 'post' } });
  check(res, {
    'POST status 201/200': (r) => r.status === 201 || r.status === 200,
  });
}

function getMetrics() {
  const url = `${BASE_URL}/api/v1/metrics/nps/${CLIENT_ID}?period=day`;
  const res = http.get(url, { headers: headers(), tags: { endpoint: 'get' } });
  check(res, { 'GET metrics 200': (r) => r.status === 200 });
}

function getResponsesWithMetadata() {
  const url = `${BASE_URL}/api/v1/clients/${CLIENT_ID}/responses/metadata?skip=0&take=50`;
  const res = http.get(url, { headers: headers(), tags: { endpoint: 'get' } });
  check(res, { 'GET responses/metadata 200': (r) => r.status === 200 });
}

function health() {
  const res = http.get(`${BASE_URL}/api/v1/health`, { tags: { endpoint: 'get' } });
  check(res, { 'health 200': (r) => r.status === 200 });
}

export default function () {
  // 50% POST, 25% metrics, 20% list, 5% health
  const p = Math.random();
  if (p < 0.5) postSurveyResponse();
  else if (p < 0.75) getMetrics();
  else if (p < 0.95) getResponsesWithMetadata();
  else health();

  sleep(0.3);
}
