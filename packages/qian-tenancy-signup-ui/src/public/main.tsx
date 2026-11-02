import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { publicClient } from '../api';
import { App } from './App';
import '../index.css';

createRoot(document.getElementById('root')!).render(<StrictMode><App client={publicClient()} /></StrictMode>);
