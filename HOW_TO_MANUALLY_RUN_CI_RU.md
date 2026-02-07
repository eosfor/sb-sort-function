# Как запустить CI workflow вручную

CI workflow настроен на запуск только вручную. Это предотвращает автоматическое выполнение при push или pull request событиях.

## Запуск workflow из вашей ветки

### Вариант 1: Через веб-интерфейс GitHub

1. Перейдите в репозиторий на GitHub: `https://github.com/eosfor/sb-sort-function`

2. Нажмите на вкладку **Actions** вверху репозитория

3. В левой боковой панели выберите workflow **"CI - Build, Deploy, Test and Cleanup"**

4. Справа вы увидите кнопку **"Run workflow"** (выпадающий список)

5. Нажмите на выпадающий список **"Run workflow"**:
   - Выберите вашу ветку из выпадающего списка "Branch" (например, `copilot/sub-pr-2` или любую другую ветку)
   - Нажмите зеленую кнопку **"Run workflow"**

6. Workflow начнет выполняться. Вы можете нажать на запущенный workflow, чтобы увидеть его прогресс и логи.

### Вариант 2: Через GitHub CLI (gh)

Если у вас установлен GitHub CLI, вы можете запустить workflow из командной строки:

```bash
# Запустить workflow на определенной ветке (замените на имя вашей ветки)
gh workflow run "CI - Build, Deploy, Test and Cleanup" --ref имя-вашей-ветки

# Пример: Запуск на ветке copilot/sub-pr-2
gh workflow run "CI - Build, Deploy, Test and Cleanup" --ref copilot/sub-pr-2

# Или используя имя файла workflow
gh workflow run ci.yml --ref имя-вашей-ветки

# Посмотреть список запусков workflow
gh run list --workflow=ci.yml

# Следить за последним запуском
gh run watch
```

### Вариант 3: Через GitHub API

Вы также можете запустить workflow используя GitHub REST API:

```bash
curl -X POST \
  -H "Accept: application/vnd.github+json" \
  -H "Authorization: Bearer YOUR_GITHUB_TOKEN" \
  -H "X-GitHub-Api-Version: 2022-11-28" \
  https://api.github.com/repos/eosfor/sb-sort-function/actions/workflows/ci.yml/dispatches \
  -d '{"ref":"имя-вашей-ветки"}'
```

Замените `имя-вашей-ветки` на фактическое имя ветки, на которой вы хотите запустить workflow.

## Предварительные требования

Перед запуском workflow убедитесь:
- Секрет `AZURE_CREDENTIALS` правильно настроен в настройках репозитория
- У вас есть необходимые права для запуска workflows в репозитории
- Все необходимые Azure ресурсы и права настроены как описано в `CI_SETUP.md`

## Что делает workflow

Ручной CI workflow выполняет следующие шаги:
1. **Build Application**: Восстанавливает зависимости, собирает и публикует .NET Function App
2. **Deploy Infrastructure and Applications**: Разворачивает Azure инфраструктуру с помощью Bicep и деплоит Function Apps
3. **Test Standard Service Bus**: Запускает тесты против Service Bus уровня Standard
4. **Test Premium Service Bus**: Запускает тесты против Service Bus уровня Premium  
5. **Cleanup Resources**: Удаляет deployment stack и все управляемые ресурсы

## Просмотр результатов

После запуска workflow:
- Следите за прогрессом во вкладке GitHub Actions
- Просматривайте подробные логи для каждой задачи и шага
- Проверяйте наличие ошибок или сбоев в развертывании или тестах
- Просмотрите шаг очистки, чтобы убедиться, что ресурсы правильно удалены

## Устранение неполадок

Если workflow завершается с ошибкой:
1. Проверьте логи задачи во вкладке Actions на наличие сообщений об ошибках
2. Убедитесь, что все секреты правильно настроены
3. Убедитесь, что ваши учетные данные Azure имеют необходимые права
4. Ознакомьтесь с документом `PIPELINE_VALIDATION.md` для решения распространенных проблем
