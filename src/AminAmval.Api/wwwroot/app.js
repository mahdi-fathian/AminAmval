import {icon,esc,fa,date,time,today,digits,button,empty,roleNames,actionNames} from './ui.js';
import * as V from './views.js';
import * as D from './dialogs.js';

const S={user:null,page:'dashboard',params:{},lookups:null,dashboard:null,data:null,selected:new Set(),filtersExpanded:false,reportKind:'assets',refsKind:'categories',details:new Map(),users:new Map(),events:new Map(),renderId:0,importFile:null,importPreview:null};
let csrf='',searchTimer=null,isReloading=false;
const app=document.getElementById('app'),main=()=>document.getElementById('main-content'),modalRoot=document.getElementById('modal-root');
class HttpError extends Error{constructor(message,status,payload){super(message);this.status=status;this.payload=payload}}
async function api(url,{method='GET',body,retry=2}={}){
 const headers={Accept:'application/json'};
 if(!['GET','HEAD'].includes(method)){if(!csrf){try{await refreshCsrf()}catch{}}headers['X-CSRF-TOKEN']=csrf;}
 const form=body instanceof FormData;
 if(body!==undefined&&!form)headers['Content-Type']='application/json';
 let response,data,attempt=0;
 while(attempt<=retry){
  try{
   response=await fetch(url,{method,headers,credentials:'include',body:body===undefined?undefined:form?body:JSON.stringify(body)});
   break;
  }catch(e){
   if(attempt>=retry){throw new HttpError('اتصال با سرور برقرار نشد. اتصال شبکه را بررسی کنید.',0);}attempt++;await new Promise(r=>setTimeout(r,500*attempt));continue;}
 }
 try{data=await response.json()}catch{data={message:'پاسخ سرور قابل خواندن نیست.'}}
 if(!response.ok)throw new HttpError(data.message||'درخواست انجام نشد.',response.status,data);
 return data;
}
async function refreshCsrf(){const r=await fetch('/api/auth/csrf',{credentials:'include',cache:'no-store'});if(!r.ok)throw new Error('دریافت نشست امنیتی انجام نشد.');csrf=(await r.json()).token;}
function toast(message,error=false){const node=document.createElement('div');node.className='toast'+(error?' error':'');node.setAttribute('role',error?'alert':'status');node.innerHTML=icon(error?'warning':'checkcircle')+`<span>${esc(message)}</span>`;document.getElementById('toast-root').append(node);setTimeout(()=>node.remove(),error?8500:5000)}
function handleError(e,source){console.error('[App Error]',source||'unknown',e);if(e.status===401&&S.user){S.user=null;S.dashboard=null;S.lookups=null;S.data=null;S.importFile=null;S.importPreview=null;S.details.clear();S.users.clear();S.events.clear();S.selected.clear();csrf='';closeAll();app.innerHTML=V.login();toast('نشست شما پایان یافته یا حساب تغییر کرده است. دوباره وارد شوید.',true);refreshCsrf().catch(()=>{});return}if(e.payload?.mustChangePassword&&S.user){S.user.mustChangePassword=true;closeAll();app.innerHTML=V.passwordGate(S);return}toast(e.message||'ارتباط با سامانه برقرار نشد. اتصال خود را بررسی کنید.',true)}
async function safely(fn){try{return await fn()}catch(e){handleError(e)}}
function queryString(params){const q=new URLSearchParams();Object.entries(params).forEach(([k,v])=>{if(v!==null&&v!==undefined&&v!=='')q.set(k,String(v))});return q.toString()}
function route(){const raw=location.hash.replace(/^#\/?/,'');const [p,q='']=raw.split('?');return {page:V.pageNames[p]?p:'dashboard',params:Object.fromEntries(new URLSearchParams(q))}}
function hideProfile(){document.getElementById('profile-menu')?.classList.add('hidden');document.querySelector('.profile-button')?.setAttribute('aria-expanded','false')}
function navigate(page,params={}){clearTimeout(searchTimer);closeAll();closeMobile();hideProfile();const target='#/'+page+(queryString(params)?'?'+queryString(params):'');if(location.hash===target)return safely(renderPage);location.hash=target;}
function changeParams(p,{replace=false,clearSelection=true}={}){if(clearSelection)S.selected.clear();const next={...S.params,...p};for(const k of Object.keys(next))if(next[k]===''||next[k]===undefined||next[k]===null)delete next[k];const hash='#/'+S.page+(queryString(next)?'?'+queryString(next):'');if(replace){history.replaceState(null,'',hash);return onRoute()}if(location.hash===hash){S.params=next;return safely(renderPage)}location.hash=hash;}
async function lookups(force=false){if(V.isStaff(S)&&(force||!S.lookups))S.lookups=await api('/api/lookups');return S.lookups}
function markNav(){document.querySelectorAll('.nav-link').forEach(x=>{const on=x.dataset.nav===S.page;x.classList.toggle('active',on);on?x.setAttribute('aria-current','page'):x.removeAttribute('aria-current')});const b=document.getElementById('breadcrumb-name');if(b)b.textContent=S.page==='assets'&&!V.isStaff(S)?'اموال من':V.pageNames[S.page];document.title=(S.page==='assets'&&!V.isStaff(S)?'اموال من':V.pageNames[S.page])+' | امین اموال ناواکو';}
async function onRoute(){if(!S.user||S.user.mustChangePassword)return;const r=route();const permitted=V.isAdmin(S)?Object.keys(V.pageNames):V.isStaff(S)?['dashboard','assets','assignments','users','reports','references','operations','account']:['dashboard','assets','account'];if(!permitted.includes(r.page)){navigate('dashboard');return}if(S.page!==r.page){clearTimeout(searchTimer);hideProfile();S.selected.clear();S.filtersExpanded=['reports','audit'].includes(r.page);main()?.scrollTo(0,0);window.scrollTo({top:0,behavior:'instant'})}S.page=r.page;S.params=r.params;S.reportKind=['assets','current','unlabeled'].includes(S.params.kind)?S.params.kind:'assets';S.refsKind=S.params.kind==='departments'?'departments':'categories';if(Object.keys(S.params).some(k=>!['page','pageSize','q','sort','kind','report','status'].includes(k)))S.filtersExpanded=true;markNav();await safely(renderPage)}
async function renderPage(){if(!S.user)return;const id=++S.renderId;const active=document.activeElement;const focused=main()?.contains(active)&&active.id?{id:active.id,start:active.selectionStart,end:active.selectionEnd}:null;
 await lookups();let data,html;const p={...S.params};delete p.kind;
 switch(S.page){
 case 'dashboard':data=await api('/api/dashboard');S.dashboard=data;html=V.dashboard(S,data);break;
 case 'assets':case 'assignments':data=await api('/api/assets?'+queryString({...p,...(S.page==='assignments'?{report:'current'}:{})}));html=V.assetsPage(S,data);break;
 case 'users':data=await api('/api/users?'+queryString(p));html=V.usersPage(S,data);data.items.forEach(x=>S.users.set(x.user.id,x.user));break;
 case 'reports':data=await api('/api/assets?'+queryString({...p,report:S.reportKind}));html=V.reportsPage(S,data);break;
 case 'references':await lookups(true);html=V.refsPage(S);break;
 case 'audit':data=await api('/api/audit?'+queryString(p));data.items.forEach(x=>S.events.set(String(x.id),x));html=V.auditPage(S,data);break;
 case 'settings':data=await api('/api/system');html=V.settingsPage(S,data);break;
 case 'account':S.user=await api('/api/auth/me');data=await api('/api/auth/sessions');html=V.accountPage(S)+V.sessionPanel(data);break;
 case 'operations':data=await api('/api/operations?'+queryString(p));html=V.operationsPage(S,data);break;
 }
 if(id!==S.renderId||!S.user)return;S.data=data;main().innerHTML=html+V.footer();
 const count=document.getElementById('asset-nav-count');if(count&&S.dashboard)count.textContent=fa(S.dashboard.total);
 document.getElementById('notification-dot')?.classList.toggle('hidden',!S.dashboard?.unlabeled&&!S.dashboard?.backup?.lastError);
 if(focused){const el=document.getElementById(focused.id);if(el){el.focus({preventScroll:true});try{el.setSelectionRange(focused.start,focused.end)}catch{}}}
}
async function showApp(){if(S.user.mustChangePassword){app.innerHTML=V.passwordGate(S);return}const r=route();S.page=r.page;app.innerHTML=V.shell(S);await onRoute()}
function closeMobile(){document.getElementById('sidebar')?.classList.remove('open');document.getElementById('sidebar-backdrop')?.remove()}
function currentModal(){return modalRoot.lastElementChild}
function openModal(spec){if(spec.build)spec=spec.build(S);const prior=currentModal();if(prior){prior.inert=true;prior.setAttribute('aria-hidden','true')}app.inert=true;const layer=document.createElement('div');layer.className='modal-layer';layer.style.zIndex=100+modalRoot.children.length;layer._spec=spec;layer._returnFocus=document.activeElement;const titleId='modal-title-'+Date.now();layer.innerHTML=`<section class="modal ${spec.size||''}" role="dialog" aria-modal="true" aria-labelledby="${titleId}"><header class="modal-head"><div><h2 id="${titleId}">${esc(spec.title)}</h2>${spec.subtitle?`<p>${esc(spec.subtitle)}</p>`:''}</div><button type="button" class="icon-btn" data-action="close-modal" aria-label="بستن پنجره">${icon('close')}</button></header><div class="modal-body">${spec.body||''}</div>${spec.footer?`<footer class="modal-footer">${spec.footer}</footer>`:''}</section>`;modalRoot.append(layer);document.body.style.overflow='hidden';setTimeout(()=>{if(layer.isConnected)(layer.querySelector('input:not([type=hidden]):not([type=file]):not([disabled]),select:not([disabled]),textarea:not([disabled])')||layer.querySelector('button'))?.focus({preventScroll:true})},40);return layer;}
function closeModal(committed=false){const top=currentModal();if(!top)return;if(!committed&&top.querySelector('form[data-busy="true"],button:disabled .spinner')){toast('لطفاً تا پایان عملیات صبر کنید.');return}const spec=top._spec;if(!committed&&spec.operation&&S.user&&(V.isStaff(S)||spec.operation==='password')){api('/api/audit/cancel',{method:'POST',body:{operation:spec.operation,entityId:spec.operation==='password'?S.user.id:spec.entityId||null}}).catch(()=>{})}const focus=top._returnFocus;top.remove();const previous=currentModal();if(previous){previous.inert=false;previous.removeAttribute('aria-hidden')}else{app.inert=false;document.body.style.overflow=''}if(focus?.isConnected&&!focus.closest('[inert]'))focus.focus({preventScroll:true});}
function closeAll(){while(currentModal())closeModal(true)}
function replaceModal(spec){const top=currentModal(),focus=top?._returnFocus;closeModal(true);const layer=openModal(spec);if(focus)layer._returnFocus=focus;return layer}
async function download(url){const response=await fetch(url,{credentials:'include'});if(!response.ok){let d={};try{d=await response.json()}catch{}throw new HttpError(d.message||'دریافت فایل انجام نشد.',response.status,d)}const blob=await response.blob();const h=response.headers.get('Content-Disposition')||'';let filename='amin-report.xlsx';const match=h.match(/filename\*=UTF-8''([^;]+)/i)||h.match(/filename="?([^";]+)"?/i);if(match){try{filename=decodeURIComponent(match[1])}catch{filename=match[1]}}const urlObject=URL.createObjectURL(blob);const link=document.createElement('a');link.href=urlObject;link.download=filename;document.body.append(link);link.click();link.remove();setTimeout(()=>URL.revokeObjectURL(urlObject),60000);toast('فایل آماده شد و برای دریافت به مرورگر ارسال شد.');}
async function getAsset(id){const d=await api('/api/assets/'+id);S.details.set(id,d);d.history.forEach(x=>S.events.set(String(x.id),x));return d}
async function openAsset(id){openModal(D.assetDetail(S,await getAsset(id)))}
async function getUser(id){const d=await api('/api/users/'+id);S.users.set(id,d.user);return d}
function updateSelection(){const bar=document.getElementById('selection-bar');if(bar){bar.classList.toggle('hidden',!S.selected.size);bar.querySelector('span').textContent=fa(S.selected.size)+' مورد انتخاب شده'}document.querySelectorAll('[data-select-id]').forEach(x=>x.checked=S.selected.has(x.dataset.selectId));const all=document.querySelector('[data-select-all]');if(all){const boxes=[...document.querySelectorAll('[data-select-id]')];all.checked=boxes.length>0&&boxes.every(x=>x.checked);all.indeterminate=boxes.some(x=>x.checked)&&!all.checked}}
function showFormError(form,e){const target=form.querySelector('.form-error');if(target){target.textContent=e.message||'ارتباط با سرور برقرار نشد. دوباره تلاش کنید.';target.scrollIntoView({block:'nearest',behavior:'smooth'})}else handleError(e);}
function setButtonBusy(button,busy){if(!button)return;if(busy){button._html=button.innerHTML;button.disabled=true;button.innerHTML='<span class="spinner small"></span>در حال پردازش…'}else if(button.isConnected){button.disabled=false;if(button._html)button.innerHTML=button._html}}
async function mutationDone(message,{keepModal=false}={}){if(!keepModal)closeAll();toast(message||'عملیات با موفقیت انجام شد.');S.lookups=null;S.selected.clear();S.dashboard=await api('/api/dashboard');if(['assets','users','assignments','reports','audit'].includes(S.page)){S.params.page=1;history.replaceState(null,'','#/'+S.page+'?'+queryString(S.params))}await renderPage()}
async function uploadImage(input){const file=input.files?.[0];if(!file)return;const form=input.closest('form');if(file.size>5*1024*1024)throw new Error('حداکثر حجم تصویر ۵ مگابایت است.');const status=form.querySelector('#image-upload-status');status.textContent='در حال بارگذاری تصویر…';form.dataset.uploading='true';try{const data=new FormData();data.append('file',file);const result=await api('/api/files',{method:'POST',body:data});form.elements.imageId.value=result.id;form.querySelector('#asset-image-preview').innerHTML=`<img src="${esc(result.url)}" alt="پیش‌نمایش تصویر اموال">`;status.textContent='تصویر با موفقیت بارگذاری شد.'}catch(e){status.textContent='بارگذاری تصویر انجام نشد.';throw e}finally{delete form.dataset.uploading}}
function newPasswordData(form){const d=Object.fromEntries(new FormData(form));if(d.newPassword!==d.confirmPassword)throw new Error('گذرواژهٔ جدید و تکرار آن یکسان نیستند.');return d}
async function submitForm(form,submitter){const type=form.dataset.form;if(form.dataset.busy==='true')return;form.querySelector('.form-error')?.replaceChildren();form.dataset.busy='true';setButtonBusy(submitter,true);
 try{const d=Object.fromEntries(new FormData(form));let result;switch(type){
 case 'login':result=await api('/api/auth/login',{method:'POST',body:{username:digits(d.username),password:d.password}});S.user=result;S.lookups=null;await refreshCsrf();await showApp();break;
 case 'global-search':navigate('assets',{q:d.q});break;
 case 'password':case 'password-gate':{const data=newPasswordData(form);result=await api('/api/auth/password',{method:'POST',body:{currentPassword:data.currentPassword,newPassword:data.newPassword}});S.user=result;await refreshCsrf();closeAll();toast('گذرواژه تغییر کرد. نشست‌های قبلی باطل شدند.');await showApp();break;}
 case 'asset':{if(form.dataset.uploading==='true')throw new Error('لطفاً تا پایان بارگذاری تصویر صبر کنید.');if(!d.imageId)throw new Error('تصویر اموال را بارگذاری کنید.');const id=form.dataset.id;result=await api('/api/assets'+(id?'/'+id:''),{method:id?'PUT':'POST',body:{...d,purchaseDate:d.purchaseDate||null,purchaseCost:d.purchaseCost===''?null:Number(digits(d.purchaseCost).replace(/[,٬]/g,'')),hasLabel:form.elements.hasLabel.checked,version:Number(form.dataset.version)}});await mutationDone(result.message);break;}
 case 'user':{const id=form.dataset.id;result=await api('/api/users'+(id?'/'+id:''),{method:id?'PUT':'POST',body:{...d,nationalId:digits(d.nationalId),active:form.elements.active.checked,version:Number(form.dataset.version)}});await mutationDone(result.message);break;}
 case 'reference':{const kind=form.dataset.kind,id=form.dataset.id;result=await api('/api/references/'+kind+(id?'/'+id:''),{method:id?'PUT':'POST',body:{name:d.name,description:d.description||'',version:Number(form.dataset.version)}});closeModal(true);await lookups(true);const name=kind==='categories'?'categoryId':'departmentId';document.querySelectorAll('#modal-root select[name="'+name+'"]').forEach(select=>{const prev=select.value;select.innerHTML='<option value="">انتخاب کنید</option>'+S.lookups[kind].map(x=>'<option value="'+x.id+'">'+esc(x.name)+'</option>').join('');select.value=result.id||prev});await mutationDone(result.message,{keepModal:true});break;}
 case 'assignment':result=await api('/api/assets/'+form.dataset.id+'/'+(form.dataset.transfer==='true'?'transfer':'assign'),{method:'POST',body:{userId:d.targetType==='user'?d.userId:null,departmentId:d.targetType==='department'?d.departmentId:null,startedAt:d.startedAt,reference:d.reference,notes:d.notes,version:Number(form.dataset.version)}});await mutationDone(result.message);break;
 case 'return':result=await api('/api/assets/'+form.dataset.id+'/return',{method:'POST',body:{...d,version:Number(form.dataset.version)}});await mutationDone(result.message);break;
 case 'status':result=await api('/api/assets/'+form.dataset.id+(['Sold','Scrapped','Exited'].includes(d.status)?'/disposition-requests':'/status'),{method:'POST',body:{status:d.status,date:d.date,reference:d.reference,reason:d.reason,amount:d.status==='Sold'?Number(d.amount):null,version:Number(form.dataset.version)}});await mutationDone(result.message);break;
 case 'delete-asset':result=await api('/api/assets/'+form.dataset.id,{method:'DELETE',body:{reason:d.reason,version:Number(form.dataset.version)}});await mutationDone(result.message);break;
 case 'restore-asset':result=await api('/api/assets/'+form.dataset.id+'/restore',{method:'POST',body:{reason:d.reason,version:Number(form.dataset.version)}});await mutationDone(result.message);break;
 case 'operation-decision':result=await api('/api/operations/'+form.dataset.id+'/'+form.dataset.decision,{method:'POST',body:{note:d.note,version:Number(form.dataset.version)}});await mutationDone(result.message);break;
 case 'delete-reference':result=await api('/api/references/'+form.dataset.kind+'/'+form.dataset.id,{method:'DELETE',body:{reason:d.reason,version:Number(form.dataset.version)}});await mutationDone(result.message);break;
 case 'reset-password':{const data=newPasswordData(form);result=await api('/api/users/'+form.dataset.id+'/reset-password',{method:'POST',body:{newPassword:data.newPassword}});await mutationDone(result.message);break;}
 case 'asset-filters':case 'user-filters':case 'audit-filters':changeParams({...d,page:1});break;
 case 'import-preview':{const file=form.elements.file.files[0];if(!file)throw new Error('فایل Excel را انتخاب کنید.');S.importFile=file;const data=new FormData();data.append('file',file);result=await api('/api/import/'+form.dataset.kind+'?commit=false',{method:'POST',body:data});S.importPreview=result;replaceModal(D.importDialog(form.dataset.kind,result));break;}
 case 'import-commit':{if(!S.importFile||!S.importPreview?.canCommit)throw new Error('ابتدا فایل را اعتبارسنجی کنید.');const data=new FormData();data.append('file',S.importFile);result=await api('/api/import/'+form.dataset.kind+'?commit=true',{method:'POST',body:data});if(!result.committed){S.importPreview=result;replaceModal(D.importDialog(form.dataset.kind,result));break}S.importFile=null;S.importPreview=null;await mutationDone(result.message);break;}
 }
 }catch(e){if(e.status===401&&type!=='login'||e.payload?.mustChangePassword)handleError(e);else showFormError(form,e)}finally{delete form.dataset.busy;setButtonBusy(submitter,false)}
}
async function action(name,el){const id=el.dataset.id;switch(name){
 case 'refresh':await renderPage();toast('اطلاعات به‌روز شد.');break;
 case 'profile':{const menu=document.getElementById('profile-menu');menu.classList.toggle('hidden');el.setAttribute('aria-expanded',String(!menu.classList.contains('hidden')));break;}
 case 'menu':{const side=document.getElementById('sidebar');if(side.classList.contains('open')){closeMobile();break}side.classList.add('open');const back=document.createElement('div');back.id='sidebar-backdrop';back.className='sidebar-backdrop';back.dataset.action='close-menu';document.body.append(back);break;}
 case 'close-menu':closeMobile();break;
 case 'logout':await api('/api/auth/logout',{method:'POST',body:{}});S.user=null;S.lookups=null;S.dashboard=null;S.data=null;S.importFile=null;S.importPreview=null;S.details.clear();S.users.clear();S.events.clear();S.selected.clear();closeAll();csrf='';await refreshCsrf();app.innerHTML=V.login();document.title='ورود | امین اموال ناواکو';break;
 case 'toggle-password':{const input=el.parentElement.querySelector('input');input.type=input.type==='password'?'text':'password';el.innerHTML=icon(input.type==='password'?'eye':'eyeoff');el.setAttribute('aria-label',input.type==='password'?'نمایش گذرواژه':'پنهان کردن گذرواژه');break;}
 case 'help':openModal(D.helpDialog());break;
 case 'login-help':openModal({title:'راهنمای ورود',size:'small',body:`<div class="help-content"><p>نام کاربری کارکنان به‌صورت پیش‌فرض کد پرسنلی است و گذرواژهٔ اولیهٔ آنها کد ملی است. در اولین ورود باید گذرواژه را تغییر دهید.</p><h3>اگر گذرواژه را فراموش کرده‌اید</h3><p>با مدیر سامانه یا جمعدار اموال سازمان تماس بگیرید تا از پروندهٔ کاربری، رمز موقت جدید تنظیم کند. پس از ۵ ورود ناموفق، حساب ۱۵ دقیقه قفل می‌شود.</p><h3>حساب‌های مدیر و جمعدار</h3><p>اطلاعات اولیهٔ این دو حساب در تحویل امن سامانه ارائه می‌شود و در صفحهٔ عمومی ورود نمایش داده نمی‌شود.</p></div>`,footer:button('متوجه شدم','close-modal','','primary')});break;
 case 'notifications':{const d=S.dashboard||await api('/api/dashboard');S.dashboard=d;openModal({title:'اعلان‌های سامانه',subtitle:'بر اساس وضعیت واقعی اطلاعات',size:'small',body:`${d.unlabeled?`<div class="alert warning">${icon('tag')}${fa(d.unlabeled)} مورد از اموال، برچسب فیزیکی ندارند.</div>${V.isStaff(S)?button('مشاهدهٔ گزارش بدون برچسب','go-unlabeled','barcode','small'):''}`:`<div class="alert success">${icon('checkcircle')}اموال بدون برچسبی در محدودهٔ دسترسی شما وجود ندارد.</div>`}${d.backup?`<div class="alert ${d.backup.lastError?'danger':'success'}" style="margin-top:17px">${icon('backup')}${d.backup.lastError?esc(d.backup.lastError):'پشتیبان‌گیری روزانه فعال است. اجرای بعدی: '+date(d.backup.nextRun)+'، '+time(d.backup.nextRun)}</div>`:''}`,footer:button('بستن','close-modal')});break;}
 case 'operation-state':changeParams({state:el.dataset.state,page:1});break;
 case 'operation-decision':{const row=S.data.items.find(x=>x.id===id);openModal(D.operationDecision(row,el.dataset.decision));break;}
 case 'archived-assets':changeParams({archived:'true',status:'',page:1});break;
 case 'restore-asset':openModal(D.restoreAsset((await getAsset(id)).asset));break;
 case 'print-label':window.open('/api/assets/'+encodeURIComponent(id)+'/label','_blank','noopener');break;
 case 'print-receipt':window.open('/api/assignments/'+encodeURIComponent(id)+'/receipt'+(el.dataset.kind==='return'?'?kind=return':''),'_blank','noopener');break;
 case 'revoke-session':{const r=await api('/api/auth/sessions/'+id+'/revoke',{method:'POST',body:{}});toast(r.message);await renderPage();break;}
 case 'password':openModal(D.passwordDialog());break;
 case 'close-modal':closeModal();break;
 case 'go-assets':navigate('assets');break;
 case 'go-available':case 'choose-assign':navigate('assets',{status:'Available'});if(name==='choose-assign')toast('یک مال را باز کنید و «تخصیص اموال» را انتخاب کنید.');break;
 case 'go-unlabeled':navigate('reports',{kind:'unlabeled'});break;
 case 'new-asset':await lookups();openModal(D.assetForm(S));break;
 case 'asset-detail':await openAsset(id);break;
 case 'edit-asset':await lookups(true);openModal(D.assetForm(S,(await getAsset(id)).asset));break;
 case 'assign':case 'transfer':await lookups(true);openModal(D.assignmentForm((await getAsset(id)).asset,name==='transfer'));break;
 case 'return':openModal(D.returnForm((await getAsset(id)).asset));break;
 case 'change-status':openModal(D.statusForm((await getAsset(id)).asset));break;
 case 'delete-asset':openModal(D.deleteAsset((await getAsset(id)).asset));break;
 case 'detail-tab':{const d=S.details.get(id);if(d)currentModal().querySelector('.modal-body').innerHTML=D.assetDetail(S,d,el.dataset.tab).body;break;}
 case 'more-history':{const d=S.details.get(id);const before=Math.min(...d.history.map(x=>x.id));const r=await api('/api/assets/'+id+'/history?beforeId='+before+'&pageSize=100');const ids=new Set(d.history.map(x=>x.id));const fresh=r.items.filter(x=>!ids.has(x.id));d.history.push(...fresh);fresh.forEach(x=>S.events.set(String(x.id),x));if(fresh.length===0||r.total<=r.items.length)d.historyTotal=d.history.length;currentModal().querySelector('.modal-body').innerHTML=D.assetDetail(S,d,'history').body;break;}
 case 'new-user':await lookups();openModal(D.userForm(S));break;
 case 'edit-user':await lookups(true);openModal(D.userForm(S,(await getUser(id)).user));break;
 case 'user-detail':openModal(D.userDetail(S,await getUser(id)));break;
 case 'reset-password':openModal(D.resetPassword((await getUser(id)).user));break;
 case 'user-assets':navigate('assets',{userId:id});break;
 case 'user-history':navigate('audit',{scope:'users',entityIds:id});break;
 case 'new-ref':openModal(D.referenceForm(el.dataset.kind));break;
 case 'edit-ref':await lookups(true);openModal(D.referenceForm(el.dataset.kind,S.lookups[el.dataset.kind].find(x=>x.id===id)));break;
 case 'delete-ref':await lookups(true);openModal(D.deleteReference(el.dataset.kind,S.lookups[el.dataset.kind].find(x=>x.id===id)));break;
 case 'ref-assets':navigate('assets',{[el.dataset.kind==='categories'?'categoryId':'departmentId']:id});break;
 case 'ref-kind':changeParams({kind:el.dataset.kind,q:'',page:1});break;
 case 'toggle-filters':S.filtersExpanded=!S.filtersExpanded;main().querySelector('.filter-form')?.classList.toggle('hidden',!S.filtersExpanded);el.setAttribute('aria-expanded',String(S.filtersExpanded));el.classList.toggle('soft',S.filtersExpanded);break;
 case 'reset-filters':S.selected.clear();changeParams(Object.fromEntries(Object.keys(S.params).map(k=>[k,k==='kind'?S.params.kind:k==='pageSize'?S.params.pageSize:''])));break;
 case 'status-tab':changeParams({status:el.dataset.status,archived:'',page:1});break;
 case 'user-active':changeParams({active:el.dataset.active,page:1});break;
 case 'page':changeParams({page:el.dataset.page},{clearSelection:false});break;
 case 'clear-selection':S.selected.clear();updateSelection();break;
 case 'selected-history':navigate('audit',{scope:el.dataset.scope,entityIds:[...S.selected].join(',')});break;
 case 'report-kind':navigate('reports',{kind:el.dataset.kind});break;
 case 'export':{const kind=el.dataset.kind;const p={...S.params};delete p.kind;await download('/api/reports/'+kind+'/export?'+queryString(p));break;}
 case 'download':await download(el.dataset.url);break;
 case 'import':await lookups();S.importFile=null;S.importPreview=null;openModal(D.importDialog(el.dataset.kind));break;
 case 'import-again':S.importFile=null;S.importPreview=null;replaceModal(D.importDialog(el.dataset.kind));break;
 case 'audit-detail':{const item=S.events.get(String(id));if(item)openModal(D.auditDetail(item));else throw new Error('این رویداد در داده‌های فعلی نیست. فهرست را تازه‌سازی کنید.');break;}
 case 'backup':openModal({title:'تهیهٔ نسخهٔ پشتیبان',subtitle:'پایگاه داده، تصاویر و کلیدهای نشست',size:'small',operation:'backup',body:`<div class="alert">${icon('backup')}یک نسخهٔ سازگار از پایگاه داده و تصاویر، به همراه فهرست صحت فایل‌ها تهیه می‌شود. بستهٔ پشتیبان فقط در اختیار مدیر قرار دارد.</div><p class="settings-note">این عملیات اطلاعات جاری را تغییر نمی‌دهد. نسخه‌ها به مدت تعیین‌شده در تنظیمات سرور نگهداری می‌شوند.</p>`,footer:button('انصراف','close-modal')+button('شروع پشتیبان‌گیری','backup-confirm','backup','primary')});break;
 case 'backup-confirm':{const r=await api('/api/backups',{method:'POST',body:{}});await mutationDone(r.message);break;}
 }
}
// Native event delegation keeps all dynamic pages keyboard accessible without inline scripts.
document.addEventListener('click',e=>{if(!e.target.closest('.profile-button'))hideProfile();const nav=e.target.closest('[data-nav]');if(nav){e.preventDefault();navigate(nav.dataset.nav);return}const el=e.target.closest('[data-action]');if(el&&!el.disabled){e.preventDefault();const a=el.dataset.action;const busy=['download','export','backup-confirm','more-history'];if(busy.includes(a)){setButtonBusy(el,true);safely(()=>action(a,el)).finally(()=>setButtonBusy(el,false))}else safely(()=>action(a,el));return}if(e.target.classList.contains('modal-layer')&&e.target===currentModal()){closeModal();return}if(!e.target.closest('.profile-wrap')){document.getElementById('profile-menu')?.classList.add('hidden');document.querySelector('.profile-button')?.setAttribute('aria-expanded','false')}});
document.addEventListener('submit',e=>{if(e.target.matches('form[data-form]')){e.preventDefault();submitForm(e.target,e.submitter)}});
document.addEventListener('input',e=>{if(e.target.matches('[data-list-search]')){clearTimeout(searchTimer);const input=e.target;searchTimer=setTimeout(()=>changeParams({q:input.value,page:1},{replace:true}),380)}});
document.addEventListener('change',e=>{const input=e.target;
 if(input.matches('[data-select-id]')){input.checked?S.selected.add(input.dataset.selectId):S.selected.delete(input.dataset.selectId);updateSelection()}
 if(input.matches('[data-select-all]')){document.querySelectorAll('[data-select-id]').forEach(x=>input.checked?S.selected.add(x.dataset.selectId):S.selected.delete(x.dataset.selectId));updateSelection()}
 if(input.id==='page-size')changeParams({pageSize:input.value,page:1});
 if(input.id==='asset-image-file')safely(()=>uploadImage(input));
 if(input.name==='targetType'){const user=input.value==='user',form=input.closest('form');const uf=form.querySelector('#assign-user-field'),df=form.querySelector('#assign-department-field');uf.classList.toggle('hidden',!user);df.classList.toggle('hidden',user);uf.querySelector('select').disabled=!user;uf.querySelector('select').required=user;df.querySelector('select').disabled=user;df.querySelector('select').required=!user}
 if(input.name==='status'&&input.closest('[data-form="status"]')){const box=input.closest('form').querySelector('#sale-amount-field');box.classList.toggle('hidden',input.value!=='Sold');box.querySelector('input').disabled=input.value!=='Sold';box.querySelector('input').required=input.value==='Sold';const submit=currentModal()?.querySelector('button[form="status-form"]');if(submit)submit.textContent=['Sold','Scrapped','Exited'].includes(input.value)?'ارسال درخواست مجوز':'ثبت تغییر وضعیت'}
 if(input.name==='personnelCode'&&input.closest('[data-form="user"]')){const username=input.closest('form').elements.username;if(!username.value&&/^[a-z0-9][a-z0-9._-]{2,59}$/i.test(digits(input.value)))username.value=digits(input.value).toLowerCase()}
 if(input.type==='date'&&input.value){const hint=input.closest('.form-field')?.querySelector('small');if(hint)hint.textContent='معادل شمسی: '+date(input.value)}
});
document.addEventListener('keydown',e=>{if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==='k'){e.preventDefault();document.getElementById('global-search')?.focus()}if(e.key==='Escape'){if(currentModal()){e.preventDefault();closeModal()}else{closeMobile();document.getElementById('profile-menu')?.classList.add('hidden')}}if(e.key==='Tab'&&currentModal()){const all=[...currentModal().querySelectorAll('a[href],button:not([disabled]),input:not([type=hidden]):not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex="0"]')].filter(x=>x.getClientRects().length&&!x.closest('.hidden'));if(!all.length)return;const first=all[0],last=all.at(-1);if(e.shiftKey&&document.activeElement===first){last.focus();e.preventDefault()}else if(!e.shiftKey&&document.activeElement===last){first.focus();e.preventDefault()}}});
window.addEventListener('hashchange',onRoute);
window.addEventListener('offline',()=>toast('اتصال شبکه قطع شده است. تغییرات ثبت‌نشده را نگه دارید و پس از اتصال دوباره تلاش کنید.',true));
window.addEventListener('online',()=>toast('اتصال شبکه برقرار شد.'));
window.addEventListener('error',e=>{if(isReloading)return;const msg=e.message+(e.filename?` (${e.filename}:${e.lineno})`:'');console.error('[Global Error]',msg,e.error);if(e.error instanceof HttpError)return;if(msg.includes('net::')||msg.includes('Failed to fetch')){toast('اتصال شبکه قطع شده است.',true);return}toast('خطایی در صفحه رخ داد: '+msg.substring(0,100),true);e.preventDefault()});
window.addEventListener('unhandledrejection',e=>{if(isReloading)return;console.error('[Unhandled Promise]',e.reason);if(e.reason instanceof HttpError)return;e.preventDefault()});
window.addEventListener('pagehide',()=>isReloading=true);
window.addEventListener('visibilitychange',()=>{if(document.visibilityState==='hidden'&&!isReloading){}});
async function boot(){try{await refreshCsrf();try{S.user=await api('/api/auth/me')}catch(e){if(e.status!==401)throw e}if(S.user)await showApp();else{app.innerHTML=V.login();document.title='ورود | امین اموال ناواکو'}}catch(e){app.innerHTML=`<main class="gate-screen"><div class="gate-card">${empty('اتصال به سامانه برقرار نشد','ممکن است سرویس در حال راه‌اندازی باشد. لطفاً صفحه را دوباره بارگذاری کنید.','warning')}<a class="btn primary full" href="/">تلاش دوباره</a></div></main>`}}
boot();
