import http from 'k6/http';
import { check, sleep } from 'k6';

// Настройки нагрузки
export const options = {
  stages: [
    { duration: '5s', target: 20 },  // 1. Разгон: за 5 сек поднимаем до 20 пользователей
    { duration: '10s', target: 50 }, // 2. Пик: держим 50 пользователей 10 секунд
    { duration: '5s', target: 0 },   // 3. Спад: плавно отключаем всех
  ],
};

export default function () {
  // host.docker.internal — это специальный адрес, чтобы Docker увидел твой локальный комп (Windows)
  // Порт 5283 — это порт, на котором работает твой бэкенд
  const res = http.get('http://localhost:5283/api/books');

  // Проверяем, что сервер ответил 200 OK (успех)
  check(res, { 'status was 200': (r) => r.status == 200 });

  // Ждем 1 секунду перед следующим запросом (имитация поведения человека)
  sleep(1);
}