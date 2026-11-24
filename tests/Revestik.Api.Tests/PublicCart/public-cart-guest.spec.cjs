// Run from repository root: node tests/Revestik.Api.Tests/PublicCart/public-cart-guest.spec.cjs
// Browser-like behavioral test; NOT a replacement for .NET or a real browser OAuth smoke.
const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const path = require('node:path');
const cartJs = fs.readFileSync(path.join(__dirname, '../../../src/Revestik.Public/wwwroot/js/public-cart.js'), 'utf8');
const wait = ms => new Promise(done => setTimeout(done, ms));
const key = 'revestik.public.cart.guest.v1';

class Element {
  constructor(tag = 'div') {
    this.tag = tag; this.children = []; this.dataset = {}; this.hidden = false;
    this.disabled = false; this.attrs = {}; this.textContent = ''; this.className = '';
  }
  append(...children) { for (const child of children) { child.parent = this; this.children.push(child); } }
  replaceChildren(...children) { this.children = []; this.append(...children); }
  setAttribute(name, value) { this.attrs[name] = value; }
  removeAttribute(name) { delete this.attrs[name]; }
  querySelector(selector) { return this.querySelectorAll(selector)[0] || null; }
  querySelectorAll(selector) { return descend(this).filter(x => matches(x, selector)); }
  closest(selector) { for (let x = this; x; x = x.parent) if (matches(x, selector)) return x; return null; }
}
const descend = el => el.children.flatMap(c => [c, ...descend(c)]);
const matches = (el, s) => {
  if (s.startsWith('.')) return el.className.split(' ').includes(s.slice(1));
  if (s.startsWith('#')) return el.attrs.id === s.slice(1);
  const attr = s.match(/^\[data-([a-z-]+)\]$/);
  if (attr) return Object.prototype.hasOwnProperty.call(el.dataset, attr[1].replace(/-([a-z])/g, (_, c) => c.toUpperCase()));
  if (s === 'input[name="quantity"]') return el.tag === 'input' && el.attrs.name === 'quantity';
  if (s === 'button[type="submit"]') return el.tag === 'button' && el.attrs.type === 'submit';
  return false;
};
function marked(s, tag='div') {
  const e = new Element(tag);
  const match = s.match(/data-([a-z-]+)/);
  if (match) e.dataset[match[1].replace(/-([a-z])/g, (_, c) => c.toUpperCase())] = '';
  return e;
}
const product = (id, slug) => ({
  id, slug, isPublished: true, brand: 'Marca', price: id===101 ? 1500 : 25,
  currency: id === 101 ? 'CRC' : 'USD', salesUnit: 'm²', images: [{id: 12, role: 'ProductCover'}],
  translations: [{ languageCode: 'es', name: `Producto ${id}` }]
});
const toServer = (p, quantity, version) => ({
  productId:p.id, slug:p.slug, isAvailable:true, quantity, version, brand:p.brand,
  price:p.price, currency:p.currency, salesUnit:p.salesUnit, coverImageId:p.coverImageId,
  translations:p.translations
});

function runApp({mode='guest',storage=new Map(),serverItems=[],missing=[],failProductId=null,lang='es'}={}) {
  const calls=[];
  const root=new Element('html');
  const nav=marked('[data-public-cart-link]', 'a'); nav.dataset.publicCartSession=mode;
  nav.append(marked('[data-public-cart-count]', 'span')); root.append(nav);
  const quickA=marked('[data-public-cart-add]', 'button'); quickA.dataset.productId='101'; quickA.dataset.productSlug='porcelanato'; root.append(quickA);
  const quickB=marked('[data-public-cart-add]', 'button'); quickB.dataset.productId='102'; quickB.dataset.productSlug='ceramica'; root.append(quickB);
  const page=marked('[data-public-cart-view]', 'main');page.dataset.publicCartWhatsappNumber='50688888888';root.append(page);
  const sections={};
  for(const s of ['loading','empty','login','forbidden','error','results','guest-intro','import-panel']) {
    sections[s]=marked(`[data-public-cart-${s}]`);
    page.append(sections[s]);
  }
  const list=marked('[data-public-cart-items]');sections.results.append(list);
  const breakdown=marked('[data-public-cart-breakdown]'); sections.results.append(breakdown);
  const whatsappLink=marked('[data-public-cart-whatsapp-open]','a');sections.results.append(whatsappLink);
  const importButton=marked('[data-public-cart-import]','button');sections['import-panel'].append(importButton);
  const quoteButton=marked('[data-public-cart-whatsapp]','button');sections.results.append(quoteButton);
  const pdfButton=marked('[data-public-cart-pdf]','button');sections.results.append(pdfButton);
  const pdfPanel=marked('[data-public-cart-pdf-result]');pdfPanel.hidden=true;sections.results.append(pdfPanel);
  const pdfReady=marked('[data-public-cart-pdf-ready]');pdfPanel.append(pdfReady);
  const pdfDownload=marked('[data-public-cart-pdf-download]','a');pdfPanel.append(pdfDownload);
  const pdfShare=marked('[data-public-cart-pdf-share]','button');pdfPanel.append(pdfShare);
  const showButton=marked('[data-public-cart-breakdown-toggle]','button');sections.results.append(showButton);
  const notice=new Element();notice.attrs.id='public-cart-notice';notice.hidden=true;
  notice.append(marked('[data-public-cart-notice-text]'));root.append(notice);
  const listeners={};
  const document={
    readyState:'complete',createElement:tag=>new Element(tag),
    querySelector:s=>root.querySelector(s),querySelectorAll:s=>root.querySelectorAll(s),
    addEventListener(name,handler){(listeners[name]??=[]).push(handler)}
  };
  const fire=(name,target)=>{for(const callback of listeners[name]??[])callback({target,preventDefault(){}})};
  const localStorage={
    getItem:k=>storage.has(k)?storage.get(k):null,
    setItem:(k,v)=>storage.set(k,v),removeItem:k=>storage.delete(k)
  };
  const fetch=async(url,options={})=>{
    const method=options.method||'GET';calls.push({url,method,body:options.body});
    const response=(status,payload)=>({status,ok:status>=200&&status<300,json:async()=>payload});
    if(url==='/api/public/cart/proforma' && method==='POST') return {
      status:200,ok:true,headers:{get:()=> 'application/pdf'},
      blob:async()=>new Blob(['%PDF-1.7\nmock'],{type:'application/pdf'})
    };
    if(url==='/api/public/cart')return response(200,{items:serverItems});
    if(url==='/api/auth/csrf')return response(200,{requestToken:'test-csrf'});
    if(url==='/api/public/catalog/products/porcelanato')return response(missing.includes(101)?404:200, product(101,'porcelanato'));
    if(url==='/api/public/catalog/products/ceramica')return response(missing.includes(102)?404:200, product(102,'ceramica'));
    if(url.startsWith('/api/public/cart/items/')&&method==='PUT') {
      const id=Number(url.split('/').at(-1));
      const {quantity,version}=JSON.parse(options.body);
      if (id === failProductId) return response(429, {});
      const current=serverItems.find(x=>x.productId===id);
      if((current?.version??null)!==(version??null))return response(409,{});
      const next=toServer(product(id,id===101?'porcelanato':'ceramica'),quantity,`v${id}`);
      serverItems.splice(0,serverItems.length,...serverItems.filter(x=>x.productId!==id),next);
      return response(200,{items:serverItems});
    }
    if(url.startsWith('/api/public/cart/items/')&&method==='DELETE'){
      const id=Number(url.split('/').at(-1).split('?')[0]);
      serverItems.splice(0,serverItems.length,...serverItems.filter(x=>x.productId!==id));
      return response(204,null);
    }
    throw Error('unexpected request: '+method+' '+url);
  };
  const window={location:{pathname:`/${lang}/carrito`},localStorage,addEventListener(){}};
  const navigator={clipboard:{writeText:async()=>{}}};
  const URL={createObjectURL:()=> 'blob:cart-pdf-test',revokeObjectURL:()=>{}};
  class File {constructor(chunks,name,options){this.name=name;this.size=chunks.reduce((n,b)=>n+b.size,0);this.type=options.type;}}
  vm.runInNewContext(cartJs,{document,window,navigator,Element,fetch,Intl,URL,URLSearchParams,File,Blob,Number,String,Array,Math,Date,Error,JSON,Map,Set,encodeURIComponent});
  return {fire,quickA,quickB,nav,page,list,breakdown,whatsappLink,importButton,quoteButton,pdfButton,pdfPanel,pdfReady,pdfDownload,pdfShare,showButton,notice,sections,storage,calls,serverItems};
}

(async()=>{
  const storage=new Map();
  let f=runApp({storage});
  assert.equal(f.calls.length,0,'anonymous must not access private cart');
  f.fire('click',f.quickA); await wait(10);
  assert.equal(f.calls.length,1);
  assert.equal(f.calls[0].url,'/api/public/catalog/products/porcelanato');
  assert.equal(JSON.parse(storage.get(key)).items[0].quantity,1);
  f.fire('click',f.quickA); await wait(10);
  assert.equal(JSON.parse(storage.get(key)).items[0].quantity,2);
  f.fire('click',f.quickB);await wait(10);
  assert.equal(JSON.parse(storage.get(key)).items.length,2);
  assert.equal(f.nav.querySelector('[data-public-cart-count]').textContent,'2');
  assert.equal(f.calls.some(c=>c.url==='/api/public/cart'),false);
  console.log('PASS guest: add, increment, 7-day browser storage, no private requests');

  f=runApp({storage});
  assert.equal(f.calls.length,0,'reloaded guest must not access private cart');
  assert.equal(f.list.children.length,2,'guest selection restored');
  f.fire('click',f.showButton);await wait(10);
  assert.equal(f.breakdown.hidden,false);
  assert.ok(f.breakdown.querySelector('.public-cart-breakdown-pre').textContent.includes('Producto 101'));
  f.fire('click',f.quoteButton);await wait(10);
  assert.equal(f.whatsappLink.hidden,false);
  assert.match(f.whatsappLink.href,/^https:\/\/wa.me\/50688888888\?text=/);
  assert.ok(decodeURIComponent(f.whatsappLink.href.split('?text=')[1]).includes('Producto 102'));
  console.log('PASS guest: reload, verified breakdown, WhatsApp draft, multi-currency');
  f.fire('click', f.pdfButton); await wait(15);
  assert.equal(f.pdfPanel.hidden,false,'PDF must offer download after generation');
  assert.equal(f.pdfDownload.href,'blob:cart-pdf-test','PDF is held only in browser memory');
  const pdfCall = f.calls.find(c=>c.url==='/api/public/cart/proforma');
  assert.ok(pdfCall,'PDF endpoint must be called');
  const request = JSON.parse(pdfCall.body);
  assert.equal(request.language,'es');
  assert.equal(request.items.length,2);
  assert.deepEqual(Object.keys(request.items[0]).sort(),['productId','quantity'],
    'names, prices and other browser data never control the PDF');
  console.log('PASS guest: temporary PDF download, server-authoritative IDs/quantities, no PDF stored');

  const deStorage = new Map();
  const german = runApp({storage:deStorage,lang:'de'});
  german.fire('click',german.quickA); await wait(10);
  german.fire('click',german.pdfButton); await wait(15);
  const dePdf = german.calls.find(c=>c.url==='/api/public/cart/proforma');
  assert.equal(JSON.parse(dePdf.body).language, 'de');
  assert.match(german.pdfDownload.download,
    /^REVESTIK-Unverbindliche-Proforma-\d{4}-\d{2}-\d{2}\.pdf$/);
  console.log('PASS german: temporary PDF uses localized filename and language');

  f=runApp({storage,mode:'customer',serverItems:[toServer(product(101,'porcelanato'),6,'server-v1')]});
  await wait(10);
  assert.equal(f.sections['import-panel'].hidden,false,'guest selection requires explicit import');
  assert.equal(f.serverItems.length,1,'no silent import');
  f.fire('click',f.importButton); await wait(20);
  assert.equal(f.serverItems.length,2);
  assert.equal(f.serverItems.find(x=>x.productId===101).quantity,6,'existing server quantity must never be overwritten');
  assert.equal(f.serverItems.find(x=>x.productId===102).quantity,1);
  assert.equal(storage.has(key),false,'successful import clears guest selection');
  assert.equal(f.calls.filter(c=>c.method==='PUT').length,1,'only new product is written');
  console.log('PASS customer: explicit merge, preserve existing quantity, clear guest only after success');

  // Expired payload must not be read as a guest cart.
  storage.set(key,JSON.stringify({version:1,expiresAt:Date.now()-1,items:[{productId:101,slug:'porcelanato',quantity:1}]}));
  f=runApp({storage});
  assert.equal(f.nav.querySelector('[data-public-cart-count]').textContent,'');
  assert.equal(storage.has(key),false);
  console.log('PASS expired guest selection discarded');

  const partial = new Map();
  let g = runApp({storage:partial});
  g.fire('click',g.quickA);await wait(10);
  g.fire('click',g.quickB);await wait(10);
  const server = [];
  g = runApp({storage:partial,mode:'customer',serverItems:server,failProductId:102});
  await wait(10);g.fire('click',g.importButton);await wait(20);
  assert.equal(server.length,1,'first line saved even after later rate-limit');
  assert.equal(server[0].productId,101);
  assert.deepEqual(JSON.parse(partial.get(key)).items.map(x=>x.productId),[102],
    'failed line remains in browser; completed line is removed immediately');
  g=runApp({storage:partial,mode:'customer',serverItems:server});
  await wait(10);g.fire('click',g.importButton);await wait(20);
  assert.equal(server.length,2,'retry imports remaining product');
  assert.equal(partial.has(key),false,'retry clears completed guest selection');
  console.log('PASS partial import: safe retry after 429, no duplicated lines');

  const unavailableStore=new Map();
  let u=runApp({storage:unavailableStore});
  u.fire('click',u.quickA); await wait(10);
  u=runApp({storage:unavailableStore,missing:[101]});
  u.fire('click',u.quoteButton);await wait(10);
  assert.equal(u.whatsappLink.hidden,true,'unpublished guest product must not be sent to WhatsApp');
  assert.equal(unavailableStore.has(key),true,'unavailable guest product remains removable');
  console.log('PASS unpublished product: no misleading WhatsApp inquiry');

  f=runApp({storage:new Map(),mode:'customer'});
  await wait(10);
  f.fire('click',f.quickA);await wait(20);
  assert.equal(f.serverItems[0].quantity,1,'authenticated cart still works');
  assert.equal(f.calls.some(c=>c.url==='/api/auth/csrf'),true);
  console.log('PASS authenticated cart still requires CSRF and owns its persisted lines');
})().catch(e=>{console.error(e);process.exitCode=1});
