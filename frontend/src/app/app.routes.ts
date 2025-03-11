import { Routes } from '@angular/router';
import { AssistantComponent } from './components/assistant/assistant.component';
import { LoginComponent } from './components/login/login.component';
import { MainContentComponent } from './components/main-content/main-content.component';

export const appRoutes: Routes = [
  { path: '', component: LoginComponent },  // Set LoginComponent as default page
  { path: 'chat/:sessionId', component: AssistantComponent },
  { path: 'chat', component: AssistantComponent },
  { path: 'login', component: LoginComponent },
  { path: '**', redirectTo: '/login' }  // Wildcard route for undefined paths
];