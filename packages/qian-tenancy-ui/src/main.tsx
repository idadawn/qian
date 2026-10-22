import { createRoot } from 'react-dom/client';
import { PlatformClient } from '@idadawn/yue-sdk';
import { TenantPage } from './TenantPage';
import { i18n } from './i18n';
import './index.css';
const client = new PlatformClient({ baseUrl: location.origin, middleware: [async (request, next) => { request.headers.set('Accept-Language', i18n.instance.language); return next(request); }] });
createRoot(document.getElementById('root')!).render(<TenantPage client={client} />);
