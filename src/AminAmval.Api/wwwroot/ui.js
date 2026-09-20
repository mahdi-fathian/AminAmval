const paths={
 grid:'<rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/><rect x="14" y="14" width="7" height="7" rx="1.5"/>',
 box:'<path d="m12 3 9 5-9 5-9-5 9-5Z"/><path d="M3 8v9l9 5 9-5V8M12 13v9M7.5 5.5l9 5"/>',
 boxes:'<path d="m8 2 5 3-5 3-5-3 5-3Zm0 6v6M3 5v6l5 3 5-3V5m3 5 5 3-5 3-5-3m5 3v6m-5-9v6l5 3 5-3v-6"/>',
 users:'<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2m20 0v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"/><circle cx="9" cy="7" r="4"/>',
 user:'<circle cx="12" cy="8" r="4"/><path d="M4 21v-2a6 6 0 0 1 6-6h4a6 6 0 0 1 6 6v2"/>',
 transfer:'<path d="M4 7h16m-4-4 4 4-4 4M20 17H4m4-4-4 4 4 4"/>',
 return:'<path d="M9 4 4 9l5 5M4 9h10a6 6 0 0 1 0 12h-3"/>',
 assign:'<rect x="3" y="3" width="13" height="16" rx="2"/><path d="M7 7h5M7 11h3m5 7 3 3 5-6"/>',
 chart:'<path d="M3 3v18h18M7 16v-5m5 5V7m5 9V4"/>',
 layers:'<path d="m12 3 9 5-9 5-9-5 9-5Zm-9 9 9 5 9-5M3 16l9 5 9-5"/>',
 history:'<path d="M3 11a9 9 0 1 1 2.4 7M3 4v7h7m2-5v6l4 2"/>',
 settings:'<path d="m9 3-1 3-3 1-2 4 2 3 1 3 4 2 3-1 3-1 3-4-1-3-1-3-4-2-3 1Z"/><circle cx="11" cy="11" r="3"/>',
 search:'<circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 5 5"/>',
 calendar:'<rect x="3" y="5" width="18" height="16" rx="2"/><path d="M16 3v4M8 3v4M3 11h18M8 15h2m4 0h2m-8 3h2"/>',
 chevron:'<path d="m6 9 6 6 6-6"/>',
 left:'<path d="m14 6-6 6 6 6"/>',
 right:'<path d="m10 6 6 6-6 6"/>',
 arrow:'<path d="M20 12H4m6-6-6 6 6 6"/>',
 plus:'<path d="M12 5v14M5 12h14"/>',
 download:'<path d="M12 3v12m-5-5 5 5 5-5M4 16v5h16v-5"/>',
 upload:'<path d="M12 16V4M7 9l5-5 5 5M4 16v5h16v-5"/>',
 excel:'<path d="M14 2H5a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V9l-7-7Z"/><path d="M14 2v7h7M8 13l6 6m0-6-6 6"/>',
 file:'<path d="M14 2H5a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V9l-7-7Z"/><path d="M14 2v7h7M7 13h10M7 17h7"/>',
 shield:'<path d="M12 2 3 6v6c0 5 9 10 9 10s9-5 9-10V6l-9-4Z"/><path d="m8 12 3 3 5-6"/>',
 lock:'<rect x="4" y="10" width="16" height="11" rx="2"/><path d="M8 10V6a4 4 0 1 1 8 0v4m-4 5v2"/>',
 eye:'<path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z"/><circle cx="12" cy="12" r="3"/>',
 eyeoff:'<path d="m3 3 18 18M10.6 10.6a2 2 0 0 0 2.8 2.8M9.9 5.2c.7-.1 1.4-.2 2.1-.2 6.5 0 10 7 10 7a19 19 0 0 1-3 3.8M6.6 6.6A19 19 0 0 0 2 12s3.5 7 10 7c1.9 0 3.6-.6 5.1-1.4"/>',
 bell:'<path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 21h4"/>',
 logout:'<path d="M9 21H4V3h5m6 4 5 5-5 5M9 12h11"/>',
 home:'<path d="m3 10 9-7 9 7v11H3V10Z"/><path d="M9 21v-8h6v8"/>',
 help:'<circle cx="12" cy="12" r="9"/><path d="M9.1 9a3 3 0 0 1 5.8 1c0 2-3 3-3 3m.1 4h.01"/>',
 refresh:'<path d="M21 3v6h-6M3 21v-6h6M3 10a9 9 0 0 1 15.7-5.7L21 9M3 15l2.3 4.7A9 9 0 0 0 21 14"/>',
 check:'<path d="m5 12 4 4L19 6"/>',
 checkcircle:'<circle cx="12" cy="12" r="9"/><path d="m8 12 3 3 5-6"/>',
 close:'<path d="m6 6 12 12M6 18 18 6"/>',
 filter:'<path d="M4 7h16M7 12h10m-7 5h4"/><circle cx="8" cy="7" r="2" fill="currentColor" stroke="none"/><circle cx="15" cy="12" r="2" fill="currentColor" stroke="none"/>',
 edit:'<path d="m16 3 5 5-13 13H3v-5L16 3Zm-3 3 5 5"/>',
 trash:'<path d="M3 6h18M9 6V3h6v3M5 6l1 15h12l1-15M10 10v7m4-7v7"/>',
 info:'<circle cx="12" cy="12" r="9"/><path d="M12 11v6m0-10h.01"/>',
 warning:'<path d="m12 3 10 18H2L12 3Zm0 6v5m0 3h.01"/>',
 image:'<rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="8.5" cy="8.5" r="1.5"/><path d="m21 15-5-5L5 21"/>',
 building:'<path d="M4 21V3h11v18M15 10h5v11M2 21h20M8 7h3m-3 4h3m-3 4h3m-3 6v-3h3v3"/>',
 warehouse:'<path d="m3 9 9-6 9 6v12H3V9Z"/><path d="M7 21V11h10v10M7 15h10M7 18h10"/>',
 monitor:'<rect x="2" y="3" width="20" height="14" rx="2"/><path d="M12 17v4M8 21h8M2 13h20"/>',
 clock:'<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
 tag:'<path d="M3 3h8l10 10-8 8L3 11V3Z"/><circle cx="7.5" cy="7.5" r="1"/>',
 barcode:'<path d="M3 4v16m4-16v16m3-16v16m4-16v16m3-16v16m4-16v16"/>',
 database:'<ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M3 5v14c0 4 18 4 18 0V5M3 12c0 4 18 4 18 0"/>',
 backup:'<path d="M5 18a6 6 0 0 1-2-11 8 8 0 0 1 15-1 6 6 0 0 1 1 12M12 21V10m-4 4 4-4 4 4"/>',
 menu:'<path d="M4 6h16M4 12h16M4 18h16"/>',
 spark:'<path d="m12 3 2.5 6.5L21 12l-6.5 2.5L12 21l-2.5-6.5L3 12l6.5-2.5L12 3Z"/>',
 more:'<circle cx="5" cy="12" r="1"/><circle cx="12" cy="12" r="1"/><circle cx="19" cy="12" r="1"/>'
};
export function icon(name,cls=''){return `<svg class="icon ${cls}" viewBox="0 0 24 24" aria-hidden="true">${paths[name]||paths.box}</svg>`}
export function esc(v){return String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]))}
export const fa=v=>new Intl.NumberFormat('fa-IR').format(Number(v)||0);
export const digits=s=>String(s??'').replace(/[۰-۹٠-٩]/g,c=>'۰۱۲۳۴۵۶۷۸۹'.includes(c)?'۰۱۲۳۴۵۶۷۸۹'.indexOf(c):'٠١٢٣٤٥٦٧٨٩'.indexOf(c));
export function dateObj(v){if(!v)return null;const s=String(v);const d=new Date(/^\d{4}-\d{2}-\d{2}$/.test(s)?s+'T12:00:00Z':/T/.test(s)&&!/(Z|[+-]\d{2}:\d{2})$/.test(s)?s+'Z':s);return isNaN(d)?null:d}
export function date(v,full=false){const d=dateObj(v);return d?new Intl.DateTimeFormat('fa-IR',{timeZone:'Asia/Tehran',year:'numeric',month:full?'long':'2-digit',day:'2-digit',...(full?{weekday:'long'}:{})}).format(d):'—'}
export function time(v){const d=dateObj(v);return d?new Intl.DateTimeFormat('fa-IR',{timeZone:'Asia/Tehran',hour:'2-digit',minute:'2-digit',second:'2-digit',hour12:false}).format(d):'—'}
export function today(){const p=new Intl.DateTimeFormat('en-CA',{timeZone:'Asia/Tehran',year:'numeric',month:'2-digit',day:'2-digit'}).formatToParts(new Date());return ['year','month','day'].map(k=>p.find(x=>x.type===k).value).join('-')}
export const isoDate=v=>v?String(v).slice(0,10):'';
export function size(v){const n=Number(v)||0;return n>=1048576?fa((n/1048576).toFixed(1))+' مگابایت':n>=1024?fa(Math.round(n/1024))+' کیلوبایت':fa(n)+' بایت'}
export const roleNames={Admin:'مدیر سامانه',Custodian:'جمعدار اموال',Employee:'کاربر عادی'};
export const statusNames={Available:'موجود در انبار',Assigned:'در حال بهره‌برداری',Maintenance:'در تعمیر',Scrapped:'اسقاط‌شده',Sold:'فروخته‌شده',Lost:'مفقودشده',Exited:'خارج‌شده'};
export const statusColors={Available:'blue',Assigned:'green',Maintenance:'orange',Scrapped:'red',Sold:'orange',Lost:'red',Exited:'red'};
export const qualityNames={New:'نو',Good:'سالم',Used:'کارکرده',Damaged:'معیوب'};
export const actionNames={'disposition.request':'درخواست مجوز','disposition.approve':'تأیید مجوز','disposition.reject':'رد مجوز','disposition.cancel':'لغو درخواست','view.operations':'مشاهده مجوزها','restore':'بازگردانی بایگانی','label.print':'چاپ برچسب','receipt.print':'چاپ رسید','session.view':'مشاهده نشست‌ها','session.revoke':'ابطال نشست','request.rejected':'درخواست ردشده',bootstrap:'راه‌اندازی',create:'ثبت',update:'ویرایش',delete:'بایگانی / حذف',assign:'تخصیص',transfer:'انتقال',return:'عودت',status:'تغییر وضعیت',cancel:'انصراف',login:'ورود موفق','login.failed':'ورود ناموفق',logout:'خروج','password.change':'تغییر گذرواژه','password.reset':'بازنشانی گذرواژه','password.failed':'تغییر رمز ناموفق',export:'خروجی Excel','export.history':'خروجی تاریخچه',template:'دریافت قالب',upload:'بارگذاری تصویر','import.preview':'اعتبارسنجی Excel','import.create':'ثبت از Excel','import.commit':'ورود گروهی','backup.create':'پشتیبان‌گیری','backup.download':'دریافت پشتیبان','view.list':'مشاهده فهرست','view.detail':'مشاهده مشخصات','view.dashboard':'مشاهده داشبورد','view.audit':'مشاهده ممیزی','view.settings':'مشاهده تنظیمات'};
export const entityNames={Asset:'اموال',User:'کاربر',Category:'دسته‌بندی',Department:'واحد سازمانی',Image:'تصویر',System:'سامانه'};
export function badge(status){return `<span class="badge dot ${statusColors[status]||''}">${esc(statusNames[status]||status)}</span>`}
export function initials(u){return [(u?.firstName||'')[0],(u?.lastName||'')[0]].filter(Boolean).join(' ')}
export function button(text,action,ico='',cls='',attrs=''){return `<button type="button" class="btn ${cls}" data-action="${action}" ${attrs}>${ico?icon(ico):''}${esc(text)}</button>`}
export function empty(title,desc,ico='box',action='',compact=false){return `<div class="empty ${compact?'compact':''}"><div class="empty-art">${icon(ico)}</div><h3>${esc(title)}</h3><p>${esc(desc)}</p>${action}</div>`}
export function field(label,name,value='',options={}){const o=options;const attrs=`name="${name}" id="${o.id||'f-'+name}" ${o.required?'required':''} ${o.max?'maxlength="'+o.max+'"':''} ${o.min?'min="'+o.min+'"':''} ${o.maxDate?'max="'+o.maxDate+'"':''} ${o.autocomplete?'autocomplete="'+o.autocomplete+'"':''} ${o.attrs||''}`;return `<div class="form-field ${o.full?'full':''}"><label for="${o.id||'f-'+name}">${esc(label)} ${o.required?'<span class="required">*</span>':''}</label>${o.textarea?`<textarea ${attrs} rows="${o.rows||3}" placeholder="${esc(o.placeholder||'')}">${esc(value)}</textarea>`:o.type==='password'?`<div class="password-wrap"><input ${attrs} type="password" value="${esc(value)}" placeholder="${esc(o.placeholder||'')}" dir="ltr"><button type="button" data-action="toggle-password" aria-label="نمایش گذرواژه">${icon('eye')}</button></div>`:`<input ${attrs} type="${o.type||'text'}" value="${esc(value)}" placeholder="${esc(o.placeholder||'')}" ${o.ltr?'dir="ltr"':''}>`}${o.hint?`<small>${esc(o.hint)}</small>`:''}</div>`}
export function select(label,name,options,value='',settings={}){return `<div class="form-field ${settings.full?'full':''}"><label for="${settings.id||'f-'+name}">${esc(label)} ${settings.required?'<span class="required">*</span>':''}</label><div class="field-with-action"><select name="${name}" id="${settings.id||'f-'+name}" ${settings.required?'required':''} ${settings.disabled?'disabled':''} ${settings.attrs||''}><option value="">${esc(settings.placeholder||'انتخاب کنید')}</option>${options.map(x=>`<option value="${esc(x.id)}" ${String(x.id)===String(value)?'selected':''}>${esc(x.name)}</option>`).join('')}</select>${settings.quick?`<button type="button" class="btn icon-only" data-action="new-ref" data-kind="${settings.quick}" aria-label="افزودن ${esc(label)}">${icon('plus')}</button>`:''}</div>${settings.hint?`<small>${esc(settings.hint)}</small>`:''}</div>`}
export function assetArt(){return `<div class="hero-art" role="img" aria-label="تصویر مفهومی شناسنامه و مدیریت اموال"><svg viewBox="0 0 360 235" fill="none" xmlns="http://www.w3.org/2000/svg"><ellipse cx="190" cy="213" rx="140" ry="12" fill="#183BBA" opacity=".22"/><path d="M34 108v-7m-3 3.5h6M310 45v-8m-4 4h8" stroke="#AAC9FF" stroke-width="1.5"/><circle cx="290" cy="151" r="3" stroke="#A1BFFF"/><circle cx="71" cy="43" r="2" fill="#9DBDFF"/><g transform="translate(31 58) rotate(-9 60 70)"><rect x="0" y="3" width="105" height="144" rx="10" fill="#1A46C9" opacity=".38"/><rect x="0" y="0" width="105" height="139" rx="9" fill="#B7CDFF"/><rect x="11" y="12" width="83" height="60" rx="5" fill="#DBE6FF"/><path d="m37 28 15-8 16 8-16 9-15-9Zm0 0v18l15 9 16-9V28M52 37v18" stroke="#7297EF" stroke-width="2"/><rect x="15" y="85" width="64" height="4" rx="2" fill="#87A8F5"/><rect x="15" y="96" width="45" height="4" rx="2" fill="#95B3F7"/><path d="M16 115v11m5-11v11m4-11v11m5-11v11m7-11v11m5-11v11m3-11v11m7-11v11m4-11v11" stroke="#6D93ED" stroke-width="2"/></g><g transform="translate(107 35) rotate(7 96 64)"><rect x="3" y="6" width="184" height="124" rx="10" fill="#193DBC" opacity=".35"/><rect width="184" height="124" rx="8" fill="#E5EEFF"/><rect x="6" y="6" width="172" height="100" rx="5" fill="#254AB9"/><rect x="14" y="15" width="156" height="80" rx="3" fill="#3863DA"/><rect x="24" y="24" width="43" height="62" rx="4" fill="#4C75E5"/><rect x="31" y="33" width="27" height="4" rx="2" fill="#9DBBFF"/><rect x="31" y="45" width="20" height="3" rx="1.5" fill="#86A7F6"/><rect x="31" y="56" width="23" height="3" rx="1.5" fill="#86A7F6"/><rect x="31" y="67" width="18" height="3" rx="1.5" fill="#86A7F6"/><rect x="78" y="24" width="82" height="20" rx="3" fill="#547CEB"/><rect x="86" y="31" width="30" height="4" rx="2" fill="#BDD3FF"/><rect x="143" y="30" width="9" height="8" rx="2" fill="#A7C7FF"/><path d="m83 76 15-9 17 5 19-16 18 4" stroke="#91CFDD" stroke-width="3" stroke-linecap="round"/><path d="m83 76 15-9 17 5 19-16 18 4v26H83V76Z" fill="#82BAF3" opacity=".15"/><circle cx="92" cy="115" r="3" fill="#A9C2F6"/><path d="m78 124-5 21h38l-6-21" fill="#BDD2FF"/><rect x="57" y="145" width="70" height="6" rx="3" fill="#E6EEFF"/></g><g transform="translate(209 128) rotate(-5 52 36)"><rect x="3" y="5" width="108" height="73" rx="8" fill="#193EBB" opacity=".3"/><rect width="108" height="73" rx="8" fill="white"/><rect x="12" y="13" width="21" height="21" rx="4" fill="#E6EDFF"/><path d="m18 19 5-3 5 3-5 3-5-3Zm0 0v6l5 3 5-3v-6m-5 3v6" stroke="#4E73E8" stroke-width="1.2"/><rect x="41" y="15" width="46" height="4" rx="2" fill="#6887D3"/><rect x="41" y="24" width="30" height="3" rx="1.5" fill="#B0C1E7"/><path d="M14 47v14m5-14v14m3-14v14m5-14v14m6-14v14m3-14v14m5-14v14m3-14v14m6-14v14m5-14v14m3-14v14m5-14v14" stroke="#6683C9" stroke-width="2"/><rect x="82" y="47" width="12" height="12" rx="2" fill="#D31353"/></g><g><circle cx="278" cy="41" r="20" fill="#1C45C9" opacity=".25"/><circle cx="278" cy="38" r="19" fill="#D8F3F1"/><circle cx="278" cy="38" r="12" fill="#24B59D"/><path d="m273 38 3.5 3.5 6.5-7" stroke="white" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/></g><path d="M320 103h7m-3.5-3.5v7" stroke="#A6C2FF"/><circle cx="25" cy="174" r="2" fill="#A6C2FF"/></svg></div>`}
