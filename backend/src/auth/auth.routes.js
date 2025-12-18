const express = require('express');
const router = express.Router();

const { register, login, me } = require('./auth.controller');
const authMiddleware = require('./auth.middleware');

router.post('/register', register);
router.post('/login', login);
router.get('/me', authMiddleware, me);

module.exports = router;