FROM node:18

WORKDIR /app

COPY . .

RUN rm -rf node_modules package-lock.json

RUN npm install

EXPOSE 5173

CMD [ "npm", "run", "dev" ]